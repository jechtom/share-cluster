import React, { Component } from "react";
import * as Commands from '../actions/commands'
import { connect } from 'react-redux'
import { FontAwesomeIcon } from '@fortawesome/react-fontawesome'
import BasicLink from './presentational/BasicLink.jsx'
import { Link } from 'react-router-dom'

function ExtractPackage({ state, handleChange, submit, connected }) {
  const Header = () => (
    <div>
      <h2><FontAwesomeIcon icon="box-open" /> Extract Package { state.packageName } <small><FontAwesomeIcon icon="hdd" /> {state.sizeFormatted}</small></h2>
    </div>
  )

  return (
    <div>
      <Header />
      <form id="extract-package-form" className="container-fluid pb-2" onSubmit={ event => submit(event, state)}>
        <div className="row">
           <div className="form-group col-lg-12">
            <label for="path">Path</label>
            <input
              required
              type="text"
              name="path"
              className="form-control form-control-lg"
              value={state.path}
              onChange={handleChange}
            />
            <small id="nameHelp" class="form-text text-muted">Package folder will be created inside given path.</small>
          </div>
        </div>
        <div className="clearfix">
          <button type="submit" className="btn btn-primary" disabled={!connected || state.is_sending} >Extract package</button>
        </div>
      </form>
      { state.error_message && <div className="alert alert-danger">
          <FontAwesomeIcon icon="exclamation-circle" /> {state.error_message}
      </div> }
    </div>)
}



const mapStateToProps = state => ({
    state: state.ExtractPackage,
    connected: state.ServerStatus.connected
})

const mapDispatchToProps = dispatch => ({
  handleChange: (event) => dispatch(Commands.extract_package_form_change(event.target.name, event.target.value)),
  submit: (event, state) => { event.preventDefault(); dispatch(Commands.extract_package_form_submit(state)); }
})

export default connect(mapStateToProps, mapDispatchToProps)(ExtractPackage);