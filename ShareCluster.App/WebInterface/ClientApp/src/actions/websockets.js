export const connected = () => ({
    type: "WS_STATUS",
    connected: true
})

export const disconnected = () => ({
    type: "WS_STATUS",
    connected: false
})

export const onMessage = (dataString) => {
    // received in JSON
    let data = JSON.parse(dataString);
    return {
        type: data.EventName,
        data: data.EventData
    };
}