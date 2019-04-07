import React from 'react'
import { connect } from 'react-redux'
import { FontAwesomeIcon } from '@fortawesome/react-fontawesome'
import CopyLink from '../components/presentational/CopyLink.jsx'

const MyId = ({ my_id, my_id_short, className }) => {
    if(my_id === "") return ("");
    return (
        <CopyLink className={className} textCopy={"peer#" + my_id} text={my_id_short} icon="user" />
    )
}

const mapStateToProps = state => ({
    my_id_short: state.Peers.my_id_short,
    my_id: state.Peers.my_id
})

export default connect(mapStateToProps)(MyId)