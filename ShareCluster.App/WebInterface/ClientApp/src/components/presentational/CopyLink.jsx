import * as Commands from '../../actions/commands'
import { connect } from 'react-redux'
import React from "react";
import { FontAwesomeIcon } from '@fortawesome/react-fontawesome'

const CopyLink = ({textDisplay, textCopy, icon, className, clipboard_copy}) =>
    (
        <span className={ "copy-link " + className } onClick={(e) => clipboard_copy(textCopy)}>
            { icon !== undefined && <FontAwesomeIcon icon={icon} /> } {textDisplay}
        </span>
    );


const mapStateToProps = (state, ownProps) => ({
    className: ownProps.className,
    icon: ownProps.icon,
    textDisplay: ownProps.text,
    textCopy: ownProps.textCopy !== undefined ? ownProps.textCopy : ownProps.text
})

const mapDispatchToProps = dispatch => ({
    clipboard_copy: text => dispatch(Commands.clipboard_copy(text))
})
  
  export default connect(mapStateToProps, mapDispatchToProps)(CopyLink);