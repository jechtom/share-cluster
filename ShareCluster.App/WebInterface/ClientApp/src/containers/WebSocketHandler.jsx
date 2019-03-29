import React from 'react'
import { connect } from 'react-redux'
import PropTypes from 'prop-types'
import { uri_ws } from '../constants'
import WebSocket from 'react-websocket';
import { onMessage, connected, disconnected } from '../actions/websockets'; 

const WebSocketHandler = ({ dispatch }) => (

    <WebSocket 
        url={ uri_ws }
        onMessage={ (d) => dispatch(onMessage(d))}
        onOpen={ () => dispatch(connected()) }
        onClose={ () => dispatch(disconnected()) }
    />

)

export default connect()(WebSocketHandler)