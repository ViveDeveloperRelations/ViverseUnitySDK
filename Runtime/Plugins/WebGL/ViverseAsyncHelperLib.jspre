Module['ViverseAsyncHelper'] = {
    get ReturnCode() {
        return Module['ViverseReturnCodes'];
    },

    createReturnData: function(taskId, returnCode, message, payload = "") {
        return {
            TaskId: taskId,
            ReturnCode: returnCode,
            Message: message,
            Payload: payload
        };
    },

    safeCallback: function(callback, data) {
        const jsonResult = JSON.stringify(data);
        const length = lengthBytesUTF8(jsonResult) + 1;
        const ptr = _malloc(length);

        try {
            stringToUTF8(jsonResult, ptr, length);
            //if we move this back to a jslib, then this is the syntax for that
            //{{{ makeDynCall('vi', 'callback') }}}(ptr);
            
            //this doesn't work either, maybe i missed something
            //Module['dynCall_vi'](callback, ptr);
            //previously just cast it to a normal callback in the jslib files
            callback(ptr);
        } catch (e) {
            console.error("Error in callback:", e);
        } finally {
            _free(ptr);
        }
    },

    normalizeResult: function(result) {
        if (!result) return "";

        if (typeof result === 'string') return result;

        if (typeof result === 'object') {
            if ('payload' in result) {
                return typeof result.payload === 'string' ?
                    result.payload :
                    JSON.stringify(result.payload);
            }
            return JSON.stringify(result);
        }

        return String(result);
    },

    wrapAsyncWithPayload: function(taskId, promiseOrValue, callback) {
        console.log("wrapAsyncWithPayload called with taskId:", taskId);

        if (!callback) { //todo: check to see if this is non-zero or try to validate the type more, ie that it's a number ie 13045 (a value from the debugger on one run) 
            console.error("Invalid callback provided");
            return;
        }

        if (!promiseOrValue || typeof promiseOrValue.then !== 'function') {
            console.warn("Non-promise value received, wrapping in Promise");
            promiseOrValue = Promise.resolve(promiseOrValue);
        }

        try {
            promiseOrValue
                .then(result => {
                    console.log("Promise resolved:", result);

                    let returnCode = this.ReturnCode.SUCCESS;
                    let message = "Operation completed successfully";
                    let payload = "";

                    if (result !== null && typeof result === 'object') {
                        returnCode = result.returnCode || returnCode;
                        message = result.message || message;
                        payload = this.normalizeResult(result);
                    } else {
                        if(typeof result === 'number' && !isNaN(result)) {
                            returnCode = result; //for simple return codes, we just return the number, otherwise it's likely a playload.
                        }else{
                            payload = this.normalizeResult(result);
                        }
                    }

                    const returnData = this.createReturnData(
                        taskId,
                        returnCode,
                        message,
                        payload
                    );

                    this.safeCallback(callback, returnData);
                })
                .catch(error => {
                    console.error("Promise rejected or error occurred:", error);

                    const errorData = this.createReturnData(
                        taskId,
                        this.ReturnCode.ERROR_EXCEPTION,
                        `Operation failed: ${error.message || "Unknown error"}`
                    );

                    this.safeCallback(callback, errorData);
                });
        } catch (error) {
            console.error("Early error in wrapAsyncWithPayload:", error);

            const errorData = this.createReturnData(
                taskId,
                this.ReturnCode.ERROR_EXCEPTION,
                `Early error: ${error.message || "Unknown error"}`
            );

            this.safeCallback(callback, errorData);
        }
    },

    // ====================================================
    // NETWORKING EVENT SUPPORT (Unified with async pattern)
    // ====================================================

    generateTaskId: function() {
        return 'task_' + Date.now() + '_' + Math.random().toString(36).substr(2, 9);
    },

    createEventData: function(eventCategory, eventType, payload) {
        return {
            EventCategory: eventCategory, // 1 = Matchmaking, 2 = Multiplayer
            EventType: eventType,
            EventData: payload ? JSON.stringify(payload) : '{}',
            Timestamp: Date.now()
        };
    },

    wrapEventCallback: function(taskId, eventData, callback) {
        try {
            // Use standard createReturnData format for events
            const returnData = this.createReturnData(
                taskId,
                this.ReturnCode.SUCCESS,
                "Event received successfully",
                JSON.stringify(eventData)
            );

            this.safeCallback(callback, returnData);
            console.log('Event dispatched successfully with taskId:', taskId);
        } catch (e) {
            console.error("Error in event callback:", e);
            
            const errorData = this.createReturnData(
                taskId,
                this.ReturnCode.ERROR_EXCEPTION,
                `Event dispatch error: ${e.message}`
            );
            
            this.safeCallback(callback, errorData);
        }
    },

    dispatchMatchmakingEvent: function(eventType, payload, callback, taskId = null) {
        try {
            // Generate taskId if not provided
            if (!taskId) {
                taskId = this.generateTaskId();
            }

            // Validate event type using Module.ViverseCore enum helpers
            if (Module.ViverseCore.MatchmakingEventType && 
                !Module.ViverseCore.MatchmakingEventType.isValidEventType(eventType)) {
                const errorData = this.createReturnData(
                    taskId,
                    this.ReturnCode.ERROR_INVALID_PARAMETER,
                    `Invalid matchmaking event type: ${eventType}`
                );
                this.safeCallback(callback, errorData);
                return taskId;
            }

            // Create unified event data
            const eventData = this.createEventData(1, eventType, payload); // 1 = Matchmaking category
            
            // Use standard callback pattern
            this.wrapEventCallback(taskId, eventData, callback);
            
            return taskId;
        } catch (e) {
            console.error('Error dispatching matchmaking event:', e);
            
            const errorData = this.createReturnData(
                taskId || this.generateTaskId(),
                this.ReturnCode.ERROR_EXCEPTION,
                `Exception dispatching matchmaking event: ${e.message}`
            );
            this.safeCallback(callback, errorData);
            return taskId;
        }
    },

    dispatchMultiplayerEvent: function(eventType, payload, callback, taskId = null) {
        try {
            // Generate taskId if not provided
            if (!taskId) {
                taskId = this.generateTaskId();
            }

            // Validate event type using Module.ViverseCore enum helpers
            if (Module.ViverseCore.MultiplayerEventType && 
                !Module.ViverseCore.MultiplayerEventType.isValidEventType(eventType)) {
                const errorData = this.createReturnData(
                    taskId,
                    this.ReturnCode.ERROR_INVALID_PARAMETER,
                    `Invalid multiplayer event type: ${eventType}`
                );
                this.safeCallback(callback, errorData);
                return taskId;
            }

            // Create unified event data
            const eventData = this.createEventData(2, eventType, payload); // 2 = Multiplayer category
            
            // Use standard callback pattern
            this.wrapEventCallback(taskId, eventData, callback);
            
            return taskId;
        } catch (e) {
            console.error('Error dispatching multiplayer event:', e);
            
            const errorData = this.createReturnData(
                taskId || this.generateTaskId(),
                this.ReturnCode.ERROR_EXCEPTION,
                `Exception dispatching multiplayer event: ${e.message}`
            );
            this.safeCallback(callback, errorData);
            return taskId;
        }
    },

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
    }
};