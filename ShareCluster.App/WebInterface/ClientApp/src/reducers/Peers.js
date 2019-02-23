export default function Peers(state = {peers : []}, action) {
    switch (action.type) {
      case 'PEERS_CHANGED':
        return { peers : action.data.Peers }
      default:
        return state;
    }
  }