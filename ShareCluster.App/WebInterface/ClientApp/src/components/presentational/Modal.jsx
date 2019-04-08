import React from "react";
import { connect } from 'react-redux'

const Modal = ({ header, content, footer }) => (
    <div className="modal modal-backdrop" tabIndex="-1" role="dialog">
        <div className="modal-dialog" role="document">
            <div className="modal-content">
                <div className="modal-header">
                    { header }
                </div>
                <div className="modal-body">
                    { content }
                </div>
                <div className="modal-footer">
                    { footer }
                </div>
            </div>
        </div>
    </div>
);

const mapStateToProps = (state, ownProps) => ({
    header: ownProps.header,
    content: ownProps.children,
    footer: ownProps.footer
})

export default connect(mapStateToProps)(Modal);