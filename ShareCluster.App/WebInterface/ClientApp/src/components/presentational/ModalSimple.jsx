import React from "react";
import Modal from "./Modal.jsx"
import { connect } from 'react-redux'

const ModalSimple = ({ visible, title, handle_close, handle_confirm, content, close_text, confirm_text }) => {

    const Header = () => [
        (<h5 key="i1" className="modal-title">{title}</h5>),
        (<button key="i2" type="button" className="close float-right" onClick={handle_close}>
            <span>&times;</span>
        </button>)
    ]

    const Footer = () => [
        (<button key="i1" type="button" className="btn btn-secondary" onClick={handle_close}>{close_text}</button>),
        (<button key="i2" type="button" className="btn btn-primary" onClick={handle_confirm}>{confirm_text}</button>)
    ]

    return (visible && <Modal header={<Header />} footer={<Footer />}>{content}</Modal>);
}

const mapStateToProps = (state, ownProps) => ({
    visible: ownProps.visible,
    title:  ownProps.title,
    content: ownProps.content,
    close_text: ownProps.close_text,
    confirm_text: ownProps.confirm_text
})

const mapDispatchToProps = (dispatch, ownProps) => ({
    handle_close: ownProps.handle_close,
    handle_confirm: ownProps.handle_confirm
})

export default connect(mapStateToProps, mapDispatchToProps)(ModalSimple);