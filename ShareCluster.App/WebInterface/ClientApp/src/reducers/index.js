import { combineReducers } from 'redux'
import Packages from './Packages'
import ServerStatus from './ServerStatus'
import Peers from './Peers'
import CreatePackage from './CreatePackage'

export default combineReducers({
    Packages,
    Peers,
    ServerStatus,
    CreatePackage
})

