// Global variables for multiplayer functionality
let playClient = null;
let currentRoomId = null;
let autoActorSet = false;
let justCreatedRoom = false; // Track if user just created a room
let myRoomId = null; // Track the room ID that this user created

// Enhanced Message History Functions
function addMessageToHistory(message, isSent = false) {
    const messageHistory = document.getElementById('messageHistory');
    const emptyMessage = messageHistory.querySelector('.message-history-empty');

    // Remove empty message indicator if it exists
    if (emptyMessage) {
        emptyMessage.remove();
    }

    // Create message element
    const messageEl = document.createElement('div');
    messageEl.className = `message-item ${isSent ? 'sent' : 'received'}`;

    // Create timestamp
    const timestamp = new Date().toLocaleTimeString([], { hour: '2-digit', minute: '2-digit', second: '2-digit' });

    // Determine message content and format
    let messageContent;
    let isJsonContent = false;

    if (typeof message === 'object') {
        try {
            messageContent = JSON.stringify(message, null, 2);
            isJsonContent = true;
        } catch (e) {
            messageContent = String(message); // Fallback for non-serializable objects
        }
    } else {
        messageContent = String(message);
    }

    // Create message HTML
    messageEl.innerHTML = `
        <div class="message-header">
            <span class="message-direction ${isSent ? 'sent' : 'received'}">
                ${isSent ? '📤 Sent' : '📥 Received'}
            </span>
            <span class="message-timestamp">${timestamp}</span>
        </div>
        <div class="message-content ${isJsonContent ? 'json' : ''}">${messageContent.replace(/</g, "&lt;").replace(/>/g, "&gt;")}</div>
    `;

    // Add to history
    messageHistory.appendChild(messageEl);

    // Auto-scroll to bottom
    messageHistory.scrollTop = messageHistory.scrollHeight;

    console.log(`${isSent ? 'Sent' : 'Received'} message:`, message);
}

function clearMessageHistory() {
    const messageHistory = document.getElementById('messageHistory');
    messageHistory.innerHTML = `
        <div class="message-history-empty">
            No messages yet. Start a conversation!
        </div>
    `;
    console.log('Message history cleared');
}

// Enhanced matchmaking event listeners with better error handling
function setupMatchmakingClientEventListeners() {
    if (!globalThis.matchmakingClient) {
        console.warn("Matchmaking client not available for setting up listeners.");
        return;
    }

    globalThis.matchmakingClient.on("onConnect", () => {
        console.log("Matchmaking: SDK connected");
        displayResult('multiplayerResult', 'Matchmaking: Connected to SDK - Ready for operations (including GetAvailableRooms)');
    });

    globalThis.matchmakingClient.on("onJoinedLobby", () => {
        console.log("Matchmaking: Joined lobby");
        displayResult('multiplayerResult', 'Matchmaking: Joined lobby - Enhanced features available');
    });

    globalThis.matchmakingClient.on("onJoinRoom", (room) => {
        console.log("Matchmaking: Joined room", room);
        if (room && room.id) {
            currentRoomId = room.id;
            document.getElementById('currentRoomId').value = currentRoomId;

            // Update join room field if this is our created room or if it's empty
            const joinRoomField = document.getElementById('joinRoomId');
            if (!joinRoomField.value || room.id === myRoomId) {
                joinRoomField.value = currentRoomId;
            }

            updateRoomStatus();
            displayResult('multiplayerResult', `Matchmaking: Successfully joined room:\n${JSON.stringify(room, null, 2)}`);
        } else {
            console.error("Invalid room data received onJoinRoom:", room);
            displayResult('multiplayerResult', 'Matchmaking: Joined room but received invalid room data');
        }
    });

    globalThis.matchmakingClient.on("onRoomListUpdate", (rooms) => {
        console.log("Matchmaking: Room list updated", rooms);
        displayResult('multiplayerResult', `Matchmaking: Available rooms (${rooms?.length || 0}):\n${JSON.stringify(rooms, null, 2)}`);
        smartAutoFillJoinRoomId(rooms);
    });

    globalThis.matchmakingClient.on("onRoomActorChange", (actors) => {
        console.log("Matchmaking: Room actors changed", actors);
        displayResult('multiplayerResult', `Matchmaking: Room has ${actors?.length || 0} players:\n${JSON.stringify(actors, null, 2)}`);
    });

    globalThis.matchmakingClient.on("onRoomClosed", () => {
        console.log("Matchmaking: Room closed");
        clearRoomState();
        displayResult('multiplayerResult', 'Matchmaking: Room was closed');
    });

    globalThis.matchmakingClient.on("onError", (error) => {
        console.error("Matchmaking error:", error);
        displayResult('multiplayerResult', `Matchmaking Error: ${error.message || JSON.stringify(error)}\nPlease check your configuration and try again.`);
    });

    globalThis.matchmakingClient.on("stateChange", (state) => {
        console.log("Matchmaking: State changed to", state);
        displayResult('multiplayerResult', `Matchmaking: Connection state: ${state}`);
    });
    console.log("Matchmaking client event listeners attached.");
}

function attachMultiplayerClientSDKListeners() {
    console.log("[attachMultiplayerClientSDKListeners] Attempting to attach listeners.");
    if (!globalThis.multiplayerClient) {
        console.warn("[attachMultiplayerClientSDKListeners] Multiplayer client not available for attaching SDK listeners.");
        return;
    }

    console.log("[attachMultiplayerClientSDKListeners] multiplayerClient found. Attaching onConnected...");
    globalThis.multiplayerClient.onConnected(() => {
        console.log("Multiplayer: Client connected (onConnected event)");
        displayResult('multiplayerResult', 'Multiplayer: Successfully connected - Ready for real-time communication');
    });

    console.log("[attachMultiplayerClientSDKListeners] Attaching general.onMessage...");
    if (globalThis.multiplayerClient.general && typeof globalThis.multiplayerClient.general.onMessage === 'function') {
        globalThis.multiplayerClient.general.onMessage((message) => {
            console.log("Multiplayer: Message received via onMessage", message);
            addMessageToHistory(message, false);
            if (message && typeof message === 'object') {
                displayResult('multiplayerResult', `Multiplayer: Message received:\n${JSON.stringify(message, null, 2)}`);
            } else {
                displayResult('multiplayerResult', `Multiplayer: Invalid message format received: ${message}`);
            }
        });
        console.log("[attachMultiplayerClientSDKListeners] general.onMessage attached.");
    } else {
        console.error("[attachMultiplayerClientSDKListeners] multiplayerClient.general.onMessage is not available or not a function.");
    }

    console.log("[attachMultiplayerClientSDKListeners] Attaching networksync listeners...");
    globalThis.multiplayerClient.networksync.onNotifyPositionUpdate((data) => {
        console.log("Multiplayer: Position update", data);
        if (!data || typeof data.entity_type !== 'number') {
            console.error("Invalid position update data:", data);
            return;
        }
        if (data.entity_type === 1) {
            displayResult('multiplayerResult', `Multiplayer: Player ${data.user_id} position updated:\n${JSON.stringify(data.data, null, 2)}`);
        } else if (data.entity_type === 2) {
            displayResult('multiplayerResult', `Multiplayer: Entity ${data.entity_id} position updated:\n${JSON.stringify(data.data, null, 2)}`);
        }
    });

    globalThis.multiplayerClient.networksync.onNotifyRemove((data) => {
        console.log("Multiplayer: Entity removed", data);
        if (data.entity_type === 1) {
            displayResult('multiplayerResult', `Multiplayer: Player ${data.user_id} left the session`);
        } else if (data.entity_type === 2) {
            displayResult('multiplayerResult', `Multiplayer: Entity ${data.entity_id} was removed`);
        }
    });

    console.log("[attachMultiplayerClientSDKListeners] Attaching actionsync listeners...");
    globalThis.multiplayerClient.actionsync.onCompetition((data) => {
        console.log("Multiplayer: Competition result", data);
        if (data && data.competition) {
            const winner = data.competition.successor;
            const actionName = data.competition.action_name;
            displayResult('multiplayerResult', `Multiplayer: Competition "${actionName}" won by: ${winner}\n${JSON.stringify(data, null, 2)}`);
        } else {
            displayResult('multiplayerResult', `Multiplayer: Invalid competition result:\n${JSON.stringify(data, null, 2)}`);
        }
    });

    console.log("[attachMultiplayerClientSDKListeners] Attaching leaderboard listeners...");
    globalThis.multiplayerClient.leaderboard.onLeaderboardUpdate((data) => {
        console.log("Multiplayer: Leaderboard update", data);
        if (data && data.leaderboard && Array.isArray(data.leaderboard)) {
            displayResult('multiplayerResult', `Multiplayer: Leaderboard updated (${data.leaderboard.length} players):\n${JSON.stringify(data, null, 2)}`);
        } else {
            displayResult('multiplayerResult', `Multiplayer: Invalid leaderboard data:\n${JSON.stringify(data, null, 2)}`);
        }
    });
    console.log("Multiplayer client SDK event listeners ALL attached.");
}

// Utility functions for checking client readiness
function isMatchmakingReady() {
    return globalThis.matchmakingClient && typeof globalThis.matchmakingClient.createRoom === 'function';
}

function isMultiplayerReady() {
    return globalThis.multiplayerClient && typeof globalThis.multiplayerClient.general === 'object' && typeof globalThis.multiplayerClient.general.onMessage === 'function';
}

// Generate session ID helper
function generateSessionId() {
    return 'session_' + Date.now() + '_' + Math.random().toString(36).substr(2, 9);
}

function autoGenerateSessionId() {
    const sessionIdField = document.getElementById('sessionId');
    if (sessionIdField && !sessionIdField.value) {
        const newSessionId = generateSessionId();
        sessionIdField.value = newSessionId;
        console.log('Auto-generated session ID:', newSessionId);
    }
}

function autoFillPlayerInfo() {
    const playerNameField = document.getElementById('playerName');
    const playerLevelField = document.getElementById('playerLevel');
    const playerSkillField = document.getElementById('playerSkill');
    const roomNameField = document.getElementById('roomName');
    const roomModeField = document.getElementById('roomMode');
    const maxPlayersField = document.getElementById('maxPlayers');
    const minPlayersField = document.getElementById('minPlayers');

    if (!playerNameField.value) {
        playerNameField.value = 'Player_' + Math.random().toString(36).substr(2, 6);
        console.log('Auto-generated player name:', playerNameField.value);
    }
    if (!playerLevelField.value || parseInt(playerLevelField.value) < 1) playerLevelField.value = 1;
    if (!playerSkillField.value || parseInt(playerSkillField.value) < 1) playerSkillField.value = 1;
    if (!roomNameField.value) {
        roomNameField.value = 'Test Room';
        console.log('Auto-filled room name:', roomNameField.value);
    }
    if (!roomModeField.value) {
        roomModeField.value = 'casual';
        console.log('Auto-filled room mode:', roomModeField.value);
    }
    if (!maxPlayersField.value || parseInt(maxPlayersField.value) < 1) maxPlayersField.value = 4;
    if (!minPlayersField.value || parseInt(minPlayersField.value) < 1) minPlayersField.value = 2;

    const maxPlayers = parseInt(maxPlayersField.value);
    const minPlayers = parseInt(minPlayersField.value);
    if (minPlayers > maxPlayers) {
        minPlayersField.value = Math.min(2, maxPlayers);
        console.log('Adjusted min players to not exceed max players');
    }
}

async function autoSetActor() {
    if (!globalThis.matchmakingClient || autoActorSet) return;
    const sessionId = document.getElementById('sessionId').value;
    const playerName = document.getElementById('playerName').value;
    if (!sessionId || !playerName) {
        console.log('Cannot auto-set actor: missing session ID or player name');
        return;
    }
    const playerLevel = parseInt(document.getElementById('playerLevel').value) || 1;
    const playerSkill = parseInt(document.getElementById('playerSkill').value) || 1;

    try {
        const result = await globalThis.matchmakingClient.setActor({
            session_id: sessionId,
            name: playerName,
            properties: { level: playerLevel, skill: playerSkill }
        });
        if (result.success) {
            autoActorSet = true;
            const roomButtons = document.querySelectorAll('#createRoom, #joinRoom');
            roomButtons.forEach(btn => {
                btn.style.borderColor = '#27ae60';
                btn.title = 'Actor auto-set - ready to create/join rooms';
            });
            displayResult('multiplayerResult', `Actor auto-set successfully: ${playerName} (Level ${playerLevel}, Skill ${playerSkill})`);
            console.log('Actor auto-set successfully');
        } else {
            throw new Error(result.message || 'Failed to auto-set actor');
        }
    } catch (error) {
        console.error('Error auto-setting actor:', error);
        displayResult('multiplayerResult', `Error auto-setting actor: ${error.message || error}`);
    }
}

// NEW: Smart auto-fill that respects user's room creation and current state
function smartAutoFillJoinRoomId(rooms) {
    const joinRoomIdField = document.getElementById('joinRoomId');

    // Don't auto-fill if:
    // 1. User just created a room (should stay with their room)
    // 2. User already has a value in the field
    // 3. User is currently in a room (unless it's their created room)
    if (justCreatedRoom ||
        (joinRoomIdField && joinRoomIdField.value && joinRoomIdField.value !== '') ||
        (currentRoomId && currentRoomId !== myRoomId)) {
        console.log('Skipping auto-fill: user has created room, field has value, or in different room');
        return;
    }

    if (!joinRoomIdField || !rooms || rooms.length === 0) {
        return;
    }

    // Priority 1: If we have our own room in the list, prefer it
    if (myRoomId) {
        const myRoom = rooms.find(room => room.id === myRoomId);
        if (myRoom) {
            joinRoomIdField.value = myRoomId;
            console.log('Auto-filled join room ID with user\'s own room:', myRoomId);
            displayResult('multiplayerResult', `Auto-filled with your room: ${myRoom.name || myRoomId}`);
            return;
        }
    }

    // Priority 2: If no current room and field is empty, use first available room
    if (!currentRoomId) {
        const firstRoom = rooms[0];
        if (firstRoom && firstRoom.id) {
            joinRoomIdField.value = firstRoom.id;
            console.log('Auto-filled join room ID with first available room:', firstRoom.id);
            displayResult('multiplayerResult', `Auto-filled with available room: ${firstRoom.name || firstRoom.id}`);
        }
    }
}

// NEW: Clear room state when leaving/closing rooms
function clearRoomState() {
    currentRoomId = null;
    myRoomId = null;
    justCreatedRoom = false;
    document.getElementById('currentRoomId').value = '';
    document.getElementById('copyRoomId').disabled = true;
    updateRoomStatus();
}

// NEW: Update room status indicators
function updateRoomStatus() {
    const currentRoomField = document.getElementById('currentRoomId');
    const joinRoomField = document.getElementById('joinRoomId');

    if (currentRoomField) {
        // Add visual indicator if this is user's created room
        if (currentRoomId === myRoomId) {
            currentRoomField.style.backgroundColor = '#e8f5e8';
            currentRoomField.title = 'This is your created room';
        } else {
            currentRoomField.style.backgroundColor = '';
            currentRoomField.title = 'Currently joined room';
        }
    }

    if (joinRoomField && joinRoomField.value === myRoomId) {
        joinRoomField.style.backgroundColor = '#e8f5e8';
        joinRoomField.title = 'This is your created room';
    } else if (joinRoomField) {
        joinRoomField.style.backgroundColor = '';
        joinRoomField.title = 'Room ID to join';
    }
}

// NEW: Copy room ID to clipboard functionality
function copyRoomIdToClipboard() {
    if (!currentRoomId) {
        displayResult('multiplayerResult', 'No room ID to copy');
        return;
    }

    if (navigator.clipboard) {
        navigator.clipboard.writeText(currentRoomId).then(() => {
            displayResult('multiplayerResult', `Room ID copied to clipboard: ${currentRoomId}`);
        }).catch(err => {
            console.error('Failed to copy room ID:', err);
            displayResult('multiplayerResult', `Failed to copy room ID: ${err.message}`);
        });
    } else {
        // Fallback for older browsers
        const textArea = document.createElement('textarea');
        textArea.value = currentRoomId;
        document.body.appendChild(textArea);
        textArea.select();
        try {
            document.execCommand('copy');
            displayResult('multiplayerResult', `Room ID copied to clipboard: ${currentRoomId}`);
        } catch (err) {
            displayResult('multiplayerResult', `Failed to copy room ID: ${err.message}`);
        }
        document.body.removeChild(textArea);
    }
}

function isActorSet() {
    const sessionId = document.getElementById('sessionId').value;
    const playerName = document.getElementById('playerName').value;
    return sessionId && playerName;
}

function sendMessageWithHistory() {
    if (!isMultiplayerReady()) {
        displayResult('multiplayerResult', 'Multiplayer client not ready or onMessage listener not set. Initialize Multiplayer first.');
        console.warn('Attempted to send message but multiplayer client not fully ready.');
        return;
    }
    const messageText = document.getElementById('messageText').value.trim();
    if (!messageText) {
        displayResult('multiplayerResult', 'Please enter a message');
        return;
    }
    try {
        const message = {
            type: 'chat',
            text: messageText,
            timestamp: Date.now(),
            sender: document.getElementById('playerName').value || 'Anonymous'
        };
        globalThis.multiplayerClient.general.sendMessage(message);
        addMessageToHistory(message, true);
        displayResult('multiplayerResult', `Message sent successfully:\n${JSON.stringify(message, null, 2)}`);
        document.getElementById('messageText').value = '';
    } catch (error) {
        displayResult('multiplayerResult', `Error sending message: ${error.message || error}`);
        console.error("Error sending message:", error);
    }
}

function setupMultiplayerTabUIEventListeners() {
    document.getElementById('initPlay').addEventListener('click', async () => {
        if (typeof sdkLoaded === 'undefined' || !sdkLoaded) {
            displayResult('multiplayerResult', 'Core Viverse SDK not loaded yet. Please wait or refresh.');
            return;
        }
        try {
            displayResult('multiplayerResult', 'Initializing Play client...');
            if (!globalThis.viverse || !globalThis.viverse.play) {
                 throw new Error('Viverse SDK or Play module not available.');
            }
            playClient = new globalThis.viverse.play();
            await new Promise((resolve, reject) => {
                let attempts = 0;
                const interval = setInterval(() => {
                    if (playClient && playClient.playSDK) {
                        clearInterval(interval);
                        resolve();
                    } else if (attempts > 50) {
                        clearInterval(interval);
                        reject(new Error('Play SDK did not initialize in time.'));
                    }
                    attempts++;
                }, 100);
            });
            displayResult('multiplayerResult', 'Play client initialized successfully. globalThis.play is now available.');
            console.log("Play client appears to be initialized.");
        } catch (error) {
            console.error('Error initializing play client:', error);
            displayResult('multiplayerResult', `Error initializing play client: ${error.message || error}`);
        }
    });

    document.getElementById('initMatchmaking').addEventListener('click', async () => {
        if (!playClient || !playClient.playSDK) {
            displayResult('multiplayerResult', 'Play client not initialized or Play SDK not ready. Please initialize Play client first.');
            return;
        }
        const appId = document.getElementById('appId').value;
        if (!appId) {
            displayResult('multiplayerResult', 'Please enter an App ID');
            return;
        }
        try {
            displayResult('multiplayerResult', 'Initializing matchmaking client...');
            globalThis.matchmakingClient = await playClient.newMatchmakingClient(appId, false);
            if (!globalThis.matchmakingClient) {
                throw new Error('Failed to create matchmaking client (newMatchmakingClient returned null/undefined)');
            }
            setupMatchmakingClientEventListeners();
            setTimeout(autoSetActor, 1000);
            displayResult('multiplayerResult', 'Matchmaking client initialized successfully. Event listeners attached.');
            console.log("Matchmaking client initialized.");
        } catch (error) {
            console.error('Error initializing matchmaking client:', error);
            displayResult('multiplayerResult', `Error initializing matchmaking client: ${error.message || error}`);
        }
    });

    document.getElementById('setActor').addEventListener('click', async () => {
        if (!globalThis.matchmakingClient) {
            displayResult('multiplayerResult', 'Matchmaking client not initialized');
            return;
        }
        const sessionId = document.getElementById('sessionId').value;
        const playerName = document.getElementById('playerName').value;
        const playerLevel = parseInt(document.getElementById('playerLevel').value) || 1;
        const playerSkill = parseInt(document.getElementById('playerSkill').value) || 1;
        if (!sessionId || !playerName) {
            displayResult('multiplayerResult', 'Please enter session ID and player name');
            return;
        }
        try {
            const result = await globalThis.matchmakingClient.setActor({
                session_id: sessionId,
                name: playerName,
                properties: { level: playerLevel, skill: playerSkill }
            });
            if (!result.success) {
                throw new Error(result.message || 'Failed to set actor');
            }
            const roomButtons = document.querySelectorAll('#createRoom, #joinRoom');
            roomButtons.forEach(btn => {
                btn.style.borderColor = '#27ae60';
                btn.title = 'Actor is set - ready to create/join rooms';
            });
            displayResult('multiplayerResult', 'Actor set successfully. You can now create or join rooms.');
        } catch (error) {
            displayResult('multiplayerResult', `Error setting actor: ${error.message || error}`);
        }
    });

    document.getElementById('createRoom').addEventListener('click', async () => {
        if (!isMatchmakingReady()) {
            displayResult('multiplayerResult', 'Matchmaking client not initialized or ready.');
            return;
        }
        if (!isActorSet()) {
            displayResult('multiplayerResult', 'Please set actor information first (both session ID and player name required)');
            return;
        }
        const roomName = document.getElementById('roomName').value;
        const roomMode = document.getElementById('roomMode').value;
        const maxPlayers = parseInt(document.getElementById('maxPlayers').value) || 4;
        const minPlayers = parseInt(document.getElementById('minPlayers').value) || 2;
        if (!roomName || !roomMode) {
            displayResult('multiplayerResult', 'Please enter room name and mode');
            return;
        }
        if (minPlayers > maxPlayers) {
            displayResult('multiplayerResult', 'Minimum players cannot exceed maximum players');
            return;
        }
        try {
            const result = await globalThis.matchmakingClient.createRoom({
                name: roomName,
                mode: roomMode,
                maxPlayers: maxPlayers,
                minPlayers: minPlayers,
                properties: {}
            });
            if (result.success && result.room && result.room.id) {
                // Set room state tracking
                currentRoomId = result.room.id;
                myRoomId = result.room.id; // Track that this is our created room
                justCreatedRoom = true; // Flag that we just created a room

                // Update UI fields
                document.getElementById('currentRoomId').value = currentRoomId;
                document.getElementById('joinRoomId').value = currentRoomId; // Fill with our room ID

                // Enable copy button
                document.getElementById('copyRoomId').disabled = false;

                updateRoomStatus();

                displayResult('multiplayerResult', `Room created successfully:\n${JSON.stringify(result, null, 2)}\n\nRoom ID has been copied to "Room ID to Join" field for easy sharing.`);

                // Reset the flag after a delay to allow normal auto-fill behavior later
                setTimeout(() => {
                    justCreatedRoom = false;
                }, 3000);

            } else {
                throw new Error(result.message || 'Room creation failed or returned invalid data');
            }
        } catch (error) {
            displayResult('multiplayerResult', `Error creating room: ${error.message || error}`);
        }
    });

    document.getElementById('joinRoom').addEventListener('click', async () => {
        if (!isMatchmakingReady()) {
            displayResult('multiplayerResult', 'Matchmaking client not initialized or ready.');
            return;
        }
        if (!isActorSet()) {
            displayResult('multiplayerResult', 'Please set actor information first before joining a room');
            return;
        }
        const roomIdToJoin = document.getElementById('joinRoomId').value;
        if (!roomIdToJoin) {
            displayResult('multiplayerResult', 'Please enter a Room ID to join');
            return;
        }
        try {
            const result = await globalThis.matchmakingClient.joinRoom(roomIdToJoin);
            if (result.success && result.room && result.room.id) {
                currentRoomId = result.room.id;
                document.getElementById('currentRoomId').value = currentRoomId;
                document.getElementById('copyRoomId').disabled = false;

                // Don't update myRoomId when joining someone else's room
                // Only update if we're joining our own created room
                if (result.room.id === myRoomId) {
                    console.log('Rejoined own created room');
                }

                updateRoomStatus();
                displayResult('multiplayerResult', `Joined room successfully:\n${JSON.stringify(result, null, 2)}`);
            } else {
                console.error("Join room response missing room details or failed:", result);
                throw new Error(result.message || 'Failed to join room or room data incomplete.');
            }
        } catch (error) {
            displayResult('multiplayerResult', `Error joining room: ${error.message || error}`);
        }
    });

    document.getElementById('leaveRoom').addEventListener('click', async () => {
        if (!isMatchmakingReady()) {
            displayResult('multiplayerResult', 'Matchmaking client not initialized or ready.');
            return;
        }
        try {
            const result = await globalThis.matchmakingClient.leaveRoom();
            if (result.success) {
                if (globalThis.multiplayerClient) {
                    globalThis.multiplayerClient.disconnect();
                    globalThis.multiplayerClient = null;
                    console.log("Multiplayer client disconnected after leaving room.");
                }

                // Clear current room but keep myRoomId if we created it
                currentRoomId = null;
                document.getElementById('currentRoomId').value = '';
                document.getElementById('copyRoomId').disabled = true;
                updateRoomStatus();

                displayResult('multiplayerResult', 'Successfully left the room.');
            } else {
                throw new Error(result.message || 'Failed to leave room');
            }
        } catch (error) {
            displayResult('multiplayerResult', `Error leaving room: ${error.message || error}`);
        }
    });

    document.getElementById('closeRoom').addEventListener('click', async () => {
        if (!isMatchmakingReady()) {
            displayResult('multiplayerResult', 'Matchmaking client not initialized or ready.');
            return;
        }
        try {
            const result = await globalThis.matchmakingClient.closeRoom();
            if (result.success) {
                clearRoomState(); // Clear all room state when closing
                displayResult('multiplayerResult', `Room closed successfully: ${JSON.stringify(result, null, 2)}`);
            } else {
                 throw new Error(result.message || 'Failed to close room');
            }
        } catch (error) {
            displayResult('multiplayerResult', `Error closing room: ${error.message || error}`);
        }
    });

    document.getElementById('getAvailableRooms').addEventListener('click', async () => {
        if (!isMatchmakingReady()) {
            displayResult('multiplayerResult', 'Matchmaking client not initialized or ready.');
            return;
        }
        try {
            const result = await globalThis.matchmakingClient.getAvailableRooms();
            displayResult('multiplayerResult', result);
            if (result && result.rooms) {
                smartAutoFillJoinRoomId(result.rooms);
            } else if (Array.isArray(result)) {
                smartAutoFillJoinRoomId(result);
            }
        } catch (error) {
            displayResult('multiplayerResult', `Error getting available rooms: ${error.message || error}`);
        }
    });

    document.getElementById('getMyRoomActors').addEventListener('click', async () => {
        if (!isMatchmakingReady()) {
            displayResult('multiplayerResult', 'Matchmaking client not initialized or ready.');
            return;
        }
        try {
            const result = await globalThis.matchmakingClient.getMyRoomActors();
            displayResult('multiplayerResult', result);
        } catch (error) {
            displayResult('multiplayerResult', `Error getting room actors: ${error.message || error}`);
        }
    });

    document.getElementById('generateSessionId').addEventListener('click', () => {
        const newSessionId = generateSessionId();
        document.getElementById('sessionId').value = newSessionId;
        displayResult('multiplayerResult', `Generated session ID: ${newSessionId}`);
    });

    document.getElementById('initMultiplayer').addEventListener('click', async () => {
        console.log("[InitMultiplayerButton] Clicked.");
        if (!currentRoomId) {
            console.warn("[InitMultiplayerButton] No currentRoomId.");
            displayResult('multiplayerResult', 'No room ID available. Please create or join a room first.');
            return;
        }
        const appId = document.getElementById('appId').value;
        if (!appId) {
            console.warn("[InitMultiplayerButton] No appId.");
            displayResult('multiplayerResult', 'Please enter an App ID');
            return;
        }
        console.log(`[InitMultiplayerButton] currentRoomId: ${currentRoomId}, appId: ${appId}`);

        if (!globalThis.play || !globalThis.play.MultiplayerClient) {
             console.error('[InitMultiplayerButton] globalThis.play or globalThis.play.MultiplayerClient is not defined.');
             displayResult('multiplayerResult', 'Play SDK (globalThis.play or globalThis.play.MultiplayerClient) not available. Initialize Play client first.');
             return;
        }
        console.log("[InitMultiplayerButton] globalThis.play.MultiplayerClient is available.");

        try {
            console.log("[InitMultiplayerButton] Attempting to create new MultiplayerClient.");
            displayResult('multiplayerResult', 'Initializing multiplayer client...');
            globalThis.multiplayerClient = new globalThis.play.MultiplayerClient(currentRoomId, appId);
            console.log("[InitMultiplayerButton] MultiplayerClient instance created:", globalThis.multiplayerClient);

            console.log("[InitMultiplayerButton] Attempting to call multiplayerClient.init().");
            const info = await globalThis.multiplayerClient.init();
            console.log("[InitMultiplayerButton] multiplayerClient.init() returned:", info);

            if (!info || !info.session_id) {
                globalThis.multiplayerClient = null;
                console.error('[InitMultiplayerButton] Failed to initialize multiplayer session or init() returned invalid session info.', info);
                throw new Error('Failed to initialize multiplayer session or init() returned invalid session info.');
            }
            console.log("[InitMultiplayerButton] Multiplayer session initialized successfully with session_id:", info.session_id);

            console.log("[InitMultiplayerButton] Attempting to attach SDK listeners.");
            attachMultiplayerClientSDKListeners();

            displayResult('multiplayerResult', `Multiplayer client initialized successfully. SDK Listeners attached.\n${JSON.stringify(info, null, 2)}`);
            console.log("Multiplayer client initialized and SDK event listeners attached (End of initMultiplayer handler).");

        } catch (error) {
            console.error("[InitMultiplayerButton] Error during multiplayer initialization:", error, error.stack);
            displayResult('multiplayerResult', `Error initializing multiplayer client: ${error.message || JSON.stringify(error)}`);
            globalThis.multiplayerClient = null;
        }
    });

    document.getElementById('disconnectMultiplayer').addEventListener('click', () => {
        if (!globalThis.multiplayerClient) {
            displayResult('multiplayerResult', 'Multiplayer client not initialized or already disconnected.');
            return;
        }
        try {
            globalThis.multiplayerClient.disconnect();
            globalThis.multiplayerClient = null;
            displayResult('multiplayerResult', 'Multiplayer client disconnected successfully.');
            console.log("Multiplayer client disconnected by user.");
        } catch (error) {
            displayResult('multiplayerResult', `Error disconnecting multiplayer: ${error.message || error}`);
            globalThis.multiplayerClient = null;
        }
    });

    document.getElementById('updatePosition').addEventListener('click', () => {
        if (!isMultiplayerReady()) {
            displayResult('multiplayerResult', 'Multiplayer client not ready for network sync.');
            return;
        }
        const positionData = {
            x: parseFloat(document.getElementById('posX').value) || 0,
            y: parseFloat(document.getElementById('posY').value) || 0,
            z: parseFloat(document.getElementById('posZ').value) || 0,
            w: Math.random() * 100
        };
        try {
            globalThis.multiplayerClient.networksync.updateMyPosition(positionData);
            displayResult('multiplayerResult', `Position updated successfully:\n${JSON.stringify(positionData, null, 2)}`);
        } catch (error) {
            displayResult('multiplayerResult', `Error updating position: ${error.message || error}`);
        }
    });

    document.getElementById('updateEntityPosition').addEventListener('click', () => {
        if (!isMultiplayerReady()) {
            displayResult('multiplayerResult', 'Multiplayer client not ready for entity position update.');
            return;
        }
        const entityId = document.getElementById('entityId').value;
        if (!entityId) {
            displayResult('multiplayerResult', 'Please enter an Entity ID');
            return;
        }
        const positionData = {
            x: parseFloat(document.getElementById('posX').value) || 0,
            y: parseFloat(document.getElementById('posY').value) || 0,
            z: parseFloat(document.getElementById('posZ').value) || 0,
            w: Math.random() * 100
        };
        try {
            globalThis.multiplayerClient.networksync.updateEntityPosition(entityId, positionData);
            displayResult('multiplayerResult', `Entity position updated: ${entityId}\n${JSON.stringify(positionData, null, 2)}`);
        } catch (error) {
            displayResult('multiplayerResult', `Error updating entity position: ${error.message || error}`);
        }
    });

    document.getElementById('sendMessage').addEventListener('click', sendMessageWithHistory);
    document.getElementById('clearMessages').addEventListener('click', clearMessageHistory);
    document.getElementById('messageText').addEventListener('keypress', (event) => {
        if (event.key === 'Enter') {
            event.preventDefault();
            sendMessageWithHistory();
        }
    });

    // Add copy room ID button listener
    document.getElementById('copyRoomId').addEventListener('click', copyRoomIdToClipboard);

    document.getElementById('submitCompetition').addEventListener('click', () => {
        if (!isMultiplayerReady()) {
            displayResult('multiplayerResult', 'Multiplayer client not ready for competition.');
            return;
        }
        const actionName = document.getElementById('actionName').value;
        const actionMessage = document.getElementById('actionMessage').value;
        const actionId = document.getElementById('actionId').value;
        if (!actionName || !actionMessage || !actionId) {
            displayResult('multiplayerResult', 'Please fill in all action fields');
            return;
        }
        try {
            globalThis.multiplayerClient.actionsync.competition(actionName, actionMessage, actionId);
            displayResult('multiplayerResult', `Competition submitted:\nAction: ${actionName}\nMessage: ${actionMessage}\nID: ${actionId}`);
        } catch (error) {
            displayResult('multiplayerResult', `Error submitting competition: ${error.message || error}`);
        }
    });

    document.getElementById('updateLeaderboard').addEventListener('click', () => {
        if (!isMultiplayerReady()) {
            displayResult('multiplayerResult', 'Multiplayer client not ready for leaderboard update.');
            return;
        }
        const score = parseInt(document.getElementById('realtimeScore').value) || 0;
        try {
            globalThis.multiplayerClient.leaderboard.leaderboardUpdate(score);
            displayResult('multiplayerResult', `Leaderboard score updated: ${score}`);
        } catch (error) {
            displayResult('multiplayerResult', `Error updating leaderboard: ${error.message || error}`);
        }
    });
    console.log("Multiplayer tab UI event listeners have been set up.");
}

// Initialize multiplayer functionality when DOM is ready
document.addEventListener('DOMContentLoaded', () => {
    autoGenerateSessionId();
    autoFillPlayerInfo();

    // Wait for core script's setupCoreEventListeners to be defined, then setup multiplayer UI
    const checkCoreLoaded = setInterval(() => {
        if (typeof sdkLoaded !== 'undefined' && typeof setupCoreEventListeners !== 'undefined') {
            clearInterval(checkCoreLoaded);
            setupMultiplayerTabUIEventListeners();
            console.log('Multiplayer Tab UI event listeners initialized (from DOMContentLoaded).');
        }
    }, 100);
});