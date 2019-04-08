import { combineReducers } from 'redux'
import { connectRouter } from 'connected-react-router'
import Packages from './Packages'
import ServerStatus from './ServerStatus'
import Peers from './Peers'
import CreatePackage from './CreatePackage'
import ExtractPackage from './ExtractPackage'
import Tasks from './Tasks'

export default (history) => combineReducers({
    router: connectRouter(history),
    Packages,
    Peers,
    ServerStatus,
    CreatePackage,
    ExtractPackage,
    Tasks
})

