import React from 'react'
import PropTypes from 'prop-types'
import { connect } from 'react-redux'
import { FontAwesomeIcon } from '@fortawesome/react-fontawesome'

const ServerStatusBadgeFun = ({ serverStatus }) => {
    const classNameForConnect = "badge " + (serverStatus.connected ? "badge-success" : "badge-danger");
    return (
        <span class={ classNameForConnect }>
            { serverStatus.connected ? "Online" : "Disconnected" }
        </span>
    )
}

const ServerStatusHeaderFun = ({ serverStatus }) => {
    if(serverStatus.connected) return "";
    return (
        <div className="alert alert-danger">
            <FontAwesomeIcon icon="skull-crossbones" /> <strong>We're offline!</strong> Seems like local instance of ShareCluster is not running.
        </div>
    )
}

const mapStateToProps = state => ({
    serverStatus: state.ServerStatus
})

export const ServerStatusBadge = connect(mapStateToProps)(ServerStatusBadgeFun)
export const ServerStatusHeader = connect(mapStateToProps)(ServerStatusHeaderFun)