// Paws Frontend API Bridge v1.0
// This file simplifies communication between the plugin's iframe and the main Paws application.

(function() {
    let messageId = 0;
    const pendingPromises = new Map();
    const urlParams = new URLSearchParams(window.location.search);
    const thisPluginId = urlParams.get('pluginId');

    if (!thisPluginId) {
        console.error("Paws API Bridge: Could not determine pluginId from URL. Commands will fail.");
    }

    // Listen for responses from the main application
    window.addEventListener('message', (event) => {
        // We only accept messages from the parent window
        if (event.source !== window.parent) {
            return;
        }

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

    // Expose the simplified API on the window object
    window.paws = {
        executeCommand: (command, payload) => {
            return new Promise((resolve, reject) => {
                const currentId = messageId++;
                pendingPromises.set(currentId, { resolve, reject });

                window.parent.postMessage({
                    channel: 'execute-plugin-command',
                    id: currentId,
                    payload: {
                        // THE FIX: Include the plugin ID in the payload
                        pluginId: thisPluginId,
                        command: command,
                        payload: payload
                    }
                }, '*');
            });
        }
    };
})();