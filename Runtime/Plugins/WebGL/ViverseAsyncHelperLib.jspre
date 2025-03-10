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
    }
};