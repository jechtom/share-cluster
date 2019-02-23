import { combineReducers } from 'redux'
import Packages from './Packages'
import ServerStatus from './ServerStatus'
import Peers from './Peers'

export default combineReducers({
    Packages,
    Peers,
    ServerStatus
})

