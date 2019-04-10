import React from "react";
import { connect } from 'react-redux'

const BasicLink = ({ ownProps, handleClick }) => {
  return(
      <a {...ownProps} onClick={handleClick} href="#">
          {ownProps.children}
      </a>
  );
}

const mapStateToProps = (state, ownProps) => ({
  ownProps: ownProps
})

const mapDispatchToProps = (dispatch, ownProps) => ({
  handleClick: (event) => { ownProps.onClick(event); event.preventDefault(); }
})

export default connect(mapStateToProps, mapDispatchToProps)(BasicLink);