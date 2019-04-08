import React, { Component } from "react";
import * as Commands from '../actions/commands'
import { connect } from 'react-redux'
import { FontAwesomeIcon } from '@fortawesome/react-fontawesome'
import ModalSimple from './presentational/ModalSimple.jsx'

const PackagesModal = ({ delete_package_dialog, handle_delete_close, handle_delete }) => {

    return (
      <div>
          <ModalSimple 
            title="Delete confirmation" 
            visible={delete_package_dialog.visible} 
            close_text="No" 
            confirm_text="Delete" 
            content={<span>{delete_package_dialog.package_name}<br />Are you sure to delete this package?</span>}
            handle_close={handle_delete_close}
            handle_confirm={() => handle_delete(delete_package_dialog.package_id)}
            />
      </div>
    )
  }
  
  const mapStateToProps = state => ({
    delete_package_dialog: state.Packages.delete_package_dialog
  })
  
  const mapDispatchToProps = dispatch => ({
    handle_delete_close: () => dispatch(Commands.packages_delete_cancel()),
    handle_delete: (id) => dispatch(Commands.packages_delete(id))
  })
  
  export default connect(mapStateToProps, mapDispatchToProps)(PackagesModal);