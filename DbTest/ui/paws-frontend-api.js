// Paws Frontend API Bridge v2.1
// This file simplifies communication between the plugin's iframe and the main Paws application.

(function() {
    let messageId = 0;
    const pendingPromises = new Map();

    // --- FIX #1: The root cause of the plugin's failure to get its ID ---
    // The plugin ID is now passed as a URL parameter, making this reliable.
    const urlParams = new URLSearchParams(window.location.search);
    const thisPluginId = urlParams.get('pluginId');

    if (!thisPluginId) {
        console.error("Paws API Bridge: Could not determine pluginId from URL. Commands will fail.");
    }

    // Listen for responses from the main application
    window.addEventListener('message', (event) => {
        if (event.source !== window.parent) return;

        const { id, result, error } = event.data;

        if (pendingPromises.has(id)) {
            const { resolve, reject } = pendingPromises.get(id);
            if (error) {
                reject(new Error(error));
            } else {
                resolve(result);
            }
            pendingPromises.delete(id);
        }
    });

    /**
     * Sends a request to the main process via the renderer bridge.
     * @param {string} channel - The generic channel to use (e.g., 'post').
     * @param {*} [payload] - The data to send.
     * @returns {Promise<any>}
     */
    function request(channel, payload) {
        return new Promise((resolve, reject) => {
            const currentId = messageId++;
            pendingPromises.set(currentId, { resolve, reject });
            window.parent.postMessage({ channel, id: currentId, payload }, '*');
        });
    }

    // Expose the simplified and corrected API on the window object
    window.paws = {
        // --- FIX #2: This is the critical logic change. ---
        // Instead of sending a custom 'execute-plugin-command', we now use the generic 'post'
        // method and provide the exact backend API endpoint the plugin needs to talk to.
        executeCommand: (command, payload) => {
            if (!thisPluginId) {
                return Promise.reject(new Error("Cannot execute command, pluginId is missing."));
            }
            const endpoint = `/api/plugins/execute/${thisPluginId}`;
            const body = {
                commandName: command,
                payload: payload
            };
            return request('post', { endpoint, body });
        },
        
        // Expose other generic methods
        getStoreValue: (key) => request('get-store-value', key),
        setStoreValue: (key, value) => request('set-store-value', { key, value }),
        showOpenDialog: (options) => request('show-open-dialog', options),
    };
})();