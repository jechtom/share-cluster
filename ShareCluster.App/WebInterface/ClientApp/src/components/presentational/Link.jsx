import React from "react";
import PropTypes from "prop-types";

class Link extends React.Component {
  render() {
      return(
          <a className={this.props} onClick={this.props.onClick} {...this.props} href="#">
              {this.props.children}
          </a>
      );
  }
}

Link.contextTypes = {
  router: PropTypes.object
};

export default Link;