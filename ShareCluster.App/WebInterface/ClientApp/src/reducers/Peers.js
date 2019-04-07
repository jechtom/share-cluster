export default function Peers(state = {peers : [], peers_count: 0}, action) {
    switch (action.type) {
      case 'PEERS_CHANGED':
        return { 
          peers : action.data.Peers, 
          peers_count: action.data.Peers.length, 
          my_id_short: action.data.MyIdShort,
          my_id: action.data.MyId 
        }
      default:
        return state;
    }
  }