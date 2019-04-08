import React, { Component } from "react";
import * as Commands from '../actions/commands'
import { connect } from 'react-redux'
import { FontAwesomeIcon } from '@fortawesome/react-fontawesome'
import BasicLink from './presentational/BasicLink.jsx'
import { Link } from 'react-router-dom'

function CreatePackage({ state, handleChange, handleChangeCheck, handleChangeType, submit, handleWithoutGroup }) {
  const Header = () => {
    if(state.group_use) {
      return <div>
        <h2><FontAwesomeIcon icon="folder-plus" /> New Version of { state.group_name }</h2>
        <p><FontAwesomeIcon icon="info" /> You are creating package that is linked to a previous version. Also you can create <BasicLink onClick={handleWithoutGroup}>new package</BasicLink>.</p>
      </div>
    } else {
      return <div>
        <h2><FontAwesomeIcon icon="folder-plus" /> New Package</h2>
        <p><FontAwesomeIcon icon="info" /> You are creating new package. To create new version of existing package use command on <Link to={"/packages"}>packages</Link> page.</p>
      </div>
    }
  }

  return (
    <div>
      <Header />
      <form id="create-package-form" className="row container-fluid" onSubmit={ event => submit(event, state)}>
        <div className="row">
           <div className="form-group col-lg-8 col-md-12">
            <label for="path">Folder or file path</label>
            <input
              type="text"
              name="path"
              className="form-control form-control-lg"
              value={state.path}
              onChange={handleChange}
            />
            <small id="nameHelp" class="form-text text-muted">Name of most nested directory in given path will be default name of package and folder name for extraction.</small>
          </div>
          <div className="form-group col-lg-4 col-md-12">
            <label for="name">{ state.name_custom ? "Package name (custom)" : "Package name" }</label>
            <div class="input-group mb-3">
              <div class="input-group-prepend">
                <div class="input-group-text">
                  <input 
                    type="checkbox" 
                    name="name_custom"
                    checked={state.name_custom}
                    onChange={handleChangeCheck} 
                  />
                </div>
              </div>
              <input
                  type="text"
                  name="name"
                  disabled={!state.name_custom}
                  className="form-control"
                  value={state.name}
                  onChange={handleChange}
                />
            </div>
            <small id="nameHelp" class="form-text text-muted">Package name can't be changed once it is created.</small>
          </div>
          <div className="form-group col-md-12">
            <label>Package type</label>
            <div className="radio">
              <label>
                <input type="radio" name="package_type" value="archive" checked={state.package_type == "archive"} onChange={handleChangeType} />Create optimized immutable copy of folder
                <small id="nameHelp" class="form-text text-muted">Default recommended option. This option creates optimized copy of given data. This is recommended if source files can change or if there are lot of small files. Package will not be affected if source folder will be deleted or changed after package is created.</small>
              </label>
            </div>
            <div className="radio">
              <label>
                <input type="radio" name="package_type"value="reference" checked={state.package_type == "reference"} onChange={handleChangeType} />Create reference to folder
                <small id="nameHelp" class="form-text text-muted">Only reference to given folder is created. This is recommended for large immutable files - like archives or disk images. Smaller files in folder can make upload slower and any changes to data will result in broken package (cannot be downloaded).</small>
              </label>
            </div>
          </div>
        </div>
        <button type="submit" className="btn btn-primary">Create and publish package</button>
      </form>
    </div>)
  }



const mapStateToProps = state => ({
    state: state.CreatePackage
})

const mapDispatchToProps = dispatch => ({
  handleChange: (event) => dispatch(Commands.create_package_form_change(event.target.name, event.target.value)),
  handleChangeCheck: (event) => dispatch(Commands.create_package_form_change(event.target.name, event.target.checked)),
  handleChangeType: (event) => dispatch(Commands.create_package_form_change(event.target.name, event.target.value)),
  handleWithoutGroup: (event) => dispatch(Commands.create_package_form_without_group()),
  submit: (event, state) => { event.preventDefault(); dispatch(Commands.create_package_form_submit(state)); console.log("AD"); }
})

export default connect(mapStateToProps, mapDispatchToProps)(CreatePackage);