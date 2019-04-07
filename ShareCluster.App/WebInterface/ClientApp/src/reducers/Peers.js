export default function Peers(state = {peers : [], peers_count: 0, my_id: ""}, action) {
    switch (action.type) {
      case 'PEERS_CHANGED':
        return { peers : action.data.Peers, peers_count: action.data.Peers.length, my_id: action.data.MyIdShort }
      default:
        return state;
    }
  }