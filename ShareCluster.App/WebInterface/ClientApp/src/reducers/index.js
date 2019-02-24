import { combineReducers } from 'redux'
import Commands from './Commands'
import Packages from './Packages'
import ServerStatus from './ServerStatus'
import Peers from './Peers'

export default combineReducers({
    Commands,
    Packages,
    Peers,
    ServerStatus
})

