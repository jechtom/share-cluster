import React, { Component } from "react";
import { Link } from 'react-router-dom'
import BasicLink from './presentational/BasicLink.jsx'
import * as Commands from '../actions/commands'
import { connect } from 'react-redux'
import { FontAwesomeIcon } from '@fortawesome/react-fontawesome'
import CopyLink from './presentational/CopyLink.jsx'

const PackagesSearch = ({ search, handle_term_change, handle_reset, handle_key_press }) => {

  return (
    <div className="input-group">
      <div className="input-group-prepend">
        <span className="input-group-text"><FontAwesomeIcon icon="search" /></span>
      </div>
      <input type="text" className="form-control" placeholder="Package hash or name" onChange={handle_term_change} value={search.term} onKeyDown={handle_key_press} />
      { search.is_active && <div className="input-group-append">
        <button class="btn btn-danger" type="button" onClick={handle_reset}><FontAwesomeIcon icon="backspace" /></button>
      </div>}
    </div>
  )
}

const mapStateToProps = state => ({
  search: state.Packages.search
})

const mapDispatchToProps = dispatch => ({
  handle_term_change: (event) => dispatch(Commands.packages_search_change(event.target.value)),
  handle_reset: () => dispatch(Commands.packages_search_reset()),
  handle_key_press: (event) => { if(event.key === "Escape") dispatch(Commands.packages_search_reset()); }
})

export default connect(mapStateToProps, mapDispatchToProps)(PackagesSearch);