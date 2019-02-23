import React from 'react'
import PropTypes from 'prop-types'
import { connect } from 'react-redux'

const ServerStatus = ({ serverStatus }) => {
    const classNameForConnect = "alert " + (serverStatus.connected ? "alert-success" : "alert-warning");
    return <div className={ classNameForConnect }>{ serverStatus.connected ? "Server online" : "Disconnected" }</div>
}

// ServerStatus.propTypes = {
//     state: PropTypes.isRequired
// }

const mapStateToProps = state => ({
    serverStatus: state.ServerStatus
})

export default connect(mapStateToProps)(ServerStatus)