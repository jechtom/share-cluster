export default function ServerStatus(state = { connected: false }, action) {
    switch (action.type) {
      case 'WS_STATUS':
        return { connected: action.connected };     
      default:
        return state;
    }
  }