import React from 'react'
import { connect } from 'react-redux'
import { FontAwesomeIcon } from '@fortawesome/react-fontawesome'

const MyId = ({ my_id, className }) => {
    if(my_id === "") return ("");
    return (
        <span className={className}><FontAwesomeIcon icon="user" /> #{my_id}</span>
    )
}

const mapStateToProps = state => ({
    my_id: state.Peers.my_id
})

export default connect(mapStateToProps)(MyId)