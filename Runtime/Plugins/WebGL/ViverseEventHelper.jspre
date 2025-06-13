Module['ViverseEventHelper'] = {
    get ReturnCode() {
        return Module['ViverseReturnCodes'];
    },

    // Create event result data following ViverseResult<T> pattern
    createEventResult: function(eventCategory, eventType, payload, returnCode = null, message = null) {
        const timestamp = Date.now();
        const eventId = this.generateEventId();

        return {
            EventCategory: eventCategory, // 1 = Matchmaking, 2 = Multiplayer
            EventType: eventType,
            EventData: payload ? JSON.stringify(payload) : '{}',
            Timestamp: timestamp,
            EventId: eventId,
            ReturnCode: returnCode || this.ReturnCode.SUCCESS,
            Message: message || 'Event received successfully'
        };
    },

    // Generate unique event ID
    generateEventId: function() {
        return 'evt_' + Date.now() + '_' + Math.random().toString(36).substr(2, 9);
    },

    // Safe callback with proper memory management (following ViverseAsyncHelper pattern)
    safeEventCallback: function(callback, eventData) {
        if (!callback) {
            console.error("[VIVERSE_JS_ERROR] Invalid event callback provided");
            return;
        }

        let ptr = null;
        
        try {
            const jsonResult = JSON.stringify(eventData);
            
            // ROBUST LOGGING: Always log the event being sent to C#
            console.log(`[VIVERSE_JS_DISPATCH] Dispatching event to C# - ID: ${eventData.EventId}, Category: ${eventData.EventCategory}, Type: ${eventData.EventType}`);
            console.log(`[VIVERSE_JS_RAW] Raw JSON being sent to C#: ${jsonResult}`);
            
            const length = lengthBytesUTF8(jsonResult) + 1;
            ptr = _malloc(length);

            if (!ptr || ptr === 0) {
                console.error(`[VIVERSE_JS_MEMORY_ERROR] Failed to allocate ${length} bytes for event ${eventData.EventId}`);
                return;
            }

            console.log(`[VIVERSE_JS_MEMORY] Allocated ${length} bytes at pointer ${ptr}`);

            stringToUTF8(jsonResult, ptr, length);
            callback(ptr);
            console.log(`[VIVERSE_JS_SUCCESS] Event dispatched successfully to C# - ID: ${eventData.EventId}`);
        } catch (e) {
            console.error(`[VIVERSE_JS_EXCEPTION] Error in event callback for ${eventData.EventId}:`, e);
            console.error(`[VIVERSE_JS_EXCEPTION] Event data was:`, eventData);
            console.error(`[VIVERSE_JS_EXCEPTION] Pointer: ${ptr}`);
        } finally {
            // Critical: Only free memory if it was successfully allocated
            if (ptr && ptr !== 0) {
                _free(ptr);
                console.log(`[VIVERSE_JS_MEMORY] Freed memory at pointer ${ptr}`);
            }
        }
    },

    // Dispatch matchmaking event with proper ViverseResult<T> pattern
    dispatchMatchmakingEvent: function(eventType, payload, callback) {
        console.log(`[VIVERSE_JS_MATCHMAKING] Dispatching matchmaking event - Type: ${eventType}`);
        console.log(`[VIVERSE_JS_MATCHMAKING] Raw payload:`, payload);
        
        try {
            // Validate event type
            if (!Module.ViverseCore.MatchmakingEventType.isValidEventType(eventType)) {
                console.error(`[VIVERSE_JS_MATCHMAKING_ERROR] Invalid matchmaking event type: ${eventType}`);
                const errorEvent = this.createEventResult(
                    1, // Matchmaking category
                    eventType,
                    null,
                    this.ReturnCode.ERROR_INVALID_PARAMETER,
                    `Invalid matchmaking event type: ${eventType}`
                );
                this.safeEventCallback(callback, errorEvent);
                return;
            }

            // Normalize and log payload
            const normalizedPayload = this.normalizeEventPayload(payload);
            console.log(`[VIVERSE_JS_MATCHMAKING] Normalized payload:`, normalizedPayload);

            // Create successful event result
            const eventResult = this.createEventResult(
                1, // Matchmaking category
                eventType,
                normalizedPayload,
                this.ReturnCode.SUCCESS,
                `Matchmaking event received`
            );

            console.log(`[VIVERSE_JS_MATCHMAKING] Created event result:`, eventResult);
            this.safeEventCallback(callback, eventResult);

        } catch (e) {
            console.error(`[VIVERSE_JS_MATCHMAKING_EXCEPTION] Error dispatching matchmaking event:`, e);
            console.error(`[VIVERSE_JS_MATCHMAKING_EXCEPTION] Event type: ${eventType}, Payload:`, payload);
            
            // Create error event result
            const errorEvent = this.createEventResult(
                1, // Matchmaking category
                eventType,
                null,
                this.ReturnCode.ERROR_EXCEPTION,
                `Exception dispatching matchmaking event: ${e.message}`
            );
            this.safeEventCallback(callback, errorEvent);
        }
    },

    // Dispatch multiplayer event with proper ViverseResult<T> pattern
    dispatchMultiplayerEvent: function(eventType, payload, callback) {
        console.log(`[VIVERSE_JS_MULTIPLAYER] Dispatching multiplayer event - Type: ${eventType}`);
        console.log(`[VIVERSE_JS_MULTIPLAYER] Raw payload:`, payload);
        
        try {
            // Validate event type
            if (!Module.ViverseCore.MultiplayerEventType.isValidEventType(eventType)) {
                console.error(`[VIVERSE_JS_MULTIPLAYER_ERROR] Invalid multiplayer event type: ${eventType}`);
                const errorEvent = this.createEventResult(
                    2, // Multiplayer category
                    eventType,
                    null,
                    this.ReturnCode.ERROR_INVALID_PARAMETER,
                    `Invalid multiplayer event type: ${eventType}`
                );
                this.safeEventCallback(callback, errorEvent);
                return;
            }

            // Normalize and log payload
            const normalizedPayload = this.normalizeEventPayload(payload);
            console.log(`[VIVERSE_JS_MULTIPLAYER] Normalized payload:`, normalizedPayload);
            
            // Additional logging for specific multiplayer event types
            if (eventType === Module.ViverseCore.MultiplayerEventType.Position && normalizedPayload) {
                console.log(`[VIVERSE_JS_MULTIPLAYER_POSITION] Position data - entity_type: ${normalizedPayload.entity_type}, user_id: ${normalizedPayload.user_id}, entity_id: ${normalizedPayload.entity_id}`);
                if (normalizedPayload.data) {
                    console.log(`[VIVERSE_JS_MULTIPLAYER_POSITION] Position coords - x: ${normalizedPayload.data.x}, y: ${normalizedPayload.data.y}, z: ${normalizedPayload.data.z}, w: ${normalizedPayload.data.w}`);
                }
            } else if (eventType === Module.ViverseCore.MultiplayerEventType.Message && normalizedPayload) {
                console.log(`[VIVERSE_JS_MULTIPLAYER_MESSAGE] Message data - type: ${normalizedPayload.type}, sender: ${normalizedPayload.sender}, text: "${normalizedPayload.text}"`);
            }

            // Create successful event result
            const eventResult = this.createEventResult(
                2, // Multiplayer category
                eventType,
                normalizedPayload,
                this.ReturnCode.SUCCESS,
                `Multiplayer event received`
            );

            console.log(`[VIVERSE_JS_MULTIPLAYER] Created event result:`, eventResult);
            this.safeEventCallback(callback, eventResult);

        } catch (e) {
            console.error(`[VIVERSE_JS_MULTIPLAYER_EXCEPTION] Error dispatching multiplayer event:`, e);
            console.error(`[VIVERSE_JS_MULTIPLAYER_EXCEPTION] Event type: ${eventType}, Payload:`, payload);
            
            // Create error event result
            const errorEvent = this.createEventResult(
                2, // Multiplayer category
                eventType,
                null,
                this.ReturnCode.ERROR_EXCEPTION,
                `Exception dispatching multiplayer event: ${e.message}`
            );
            this.safeEventCallback(callback, errorEvent);
        }
    },

    // Helper to validate and normalize event payload
    normalizeEventPayload: function(payload) {
        if (!payload) return null;

        if (typeof payload === 'string') {
            try {
                return JSON.parse(payload);
            } catch (e) {
                console.warn('Failed to parse payload as JSON, treating as string:', payload);
                return { data: payload };
            }
        }

        if (typeof payload === 'object') {
            return payload;
        }

        // Convert primitives to object form
        return { value: payload };
    },

    // DEBUGGING: Log all available logging tags for easy console filtering
    logDebugTags: function() {
        console.log(`
[VIVERSE_DEBUG_TAGS] Available logging tags for console filtering:

=== JAVASCRIPT SIDE ===
[VIVERSE_JS_DISPATCH] - Event dispatch to C#
[VIVERSE_JS_RAW] - Raw JSON being sent to C#
[VIVERSE_JS_MEMORY] - Memory allocation/deallocation
[VIVERSE_JS_SUCCESS] - Successful event dispatch
[VIVERSE_JS_EXCEPTION] - JavaScript exceptions
[VIVERSE_JS_MATCHMAKING] - Matchmaking event processing
[VIVERSE_JS_MULTIPLAYER] - Multiplayer event processing
[VIVERSE_JS_MULTIPLAYER_POSITION] - Position update details
[VIVERSE_JS_MULTIPLAYER_MESSAGE] - Message details

=== C# SIDE ===
[VIVERSE_EVENT_RAW] - Raw JSON received from JavaScript
[VIVERSE_EVENT_PARSED] - Successfully parsed ViverseResult
[VIVERSE_EVENT_PAYLOAD] - NetworkEventData payload
[VIVERSE_EVENT_SUCCESS] - Successful event parsing
[VIVERSE_EVENT_DATA] - Event data contents
[VIVERSE_EVENT_DISPATCHED] - Event dispatched to callback
[VIVERSE_EVENT_ERROR] - Parsing/dispatch errors
[VIVERSE_EVENT_WARNING] - Warnings (missing callbacks, etc.)
[VIVERSE_EVENT_EXCEPTION] - C# exceptions

[MATCHMAKING_DISPATCH] - Matchmaking event dispatch
[MATCHMAKING_RAW_DATA] - Raw matchmaking event data
[MATCHMAKING_PARSED] - Parsed matchmaking data
[MATCHMAKING_INVOKE] - Event invocation with subscriber count

[MULTIPLAYER_DISPATCH] - Multiplayer event dispatch
[MULTIPLAYER_RAW_DATA] - Raw multiplayer event data
[MULTIPLAYER_MESSAGE_PARSED] - Parsed message details
[MULTIPLAYER_POSITION_PARSED] - Parsed position details
[MULTIPLAYER_COMPETITION_PARSED] - Parsed competition details
[MULTIPLAYER_LEADERBOARD_PARSED] - Parsed leaderboard details

=== USAGE EXAMPLES ===
To filter console logs in browser:
- All Viverse events: VIVERSE_
- JavaScript side only: VIVERSE_JS_
- C# side only: VIVERSE_EVENT_
- Multiplayer only: MULTIPLAYER_
- Errors only: _ERROR
- Raw data only: _RAW
        `);
    }
};