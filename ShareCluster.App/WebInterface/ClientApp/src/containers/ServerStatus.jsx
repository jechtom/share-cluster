import React from 'react'
import PropTypes from 'prop-types'
import { connect } from 'react-redux'

const ServerStatus = ({ serverStatus }) => {
    const classNameForConnect = "badge " + (serverStatus.connected ? "badge-success" : "badge-danger");
    return (
        <span class={ classNameForConnect }>
            { serverStatus.connected ? "Online" : "Disconnected" }
        </span>
    )
}

const mapStateToProps = state => ({
    serverStatus: state.ServerStatus
})

export default connect(mapStateToProps)(ServerStatus)