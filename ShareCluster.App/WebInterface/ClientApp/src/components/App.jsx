import React from "react";
import WebSocketHandler from '../containers/WebSocketHandler.jsx';
import { ServerStatusBadge, ServerStatusHeader } from '../containers/ServerStatus.jsx';
import MyId from '../containers/MyId.jsx';
import { FontAwesomeIcon } from '@fortawesome/react-fontawesome'
import NavLink from "./NavLink.jsx";
import { connect } from 'react-redux'
import Routes from './Routes.jsx'
import Tasks from './Tasks.jsx'
import { Link } from 'react-router-dom'

const App = ({ peers_count, local_packages_count, remote_packages_count }) => (
  <div>

    <nav className="navbar navbar-expand-lg navbar-light bg-light">
      <Link className="navbar-brand" to={`/packages`}>
        <FontAwesomeIcon icon="th-large" /> ShareCluster
        <small className="ml-2">
          <ServerStatusBadge />
        </small>
      </Link>
      <button class="navbar-toggler" type="button" data-toggle="collapse" data-target="#navbarSupportedContent" aria-controls="navbarSupportedContent" aria-expanded="false" aria-label="Toggle navigation">
        <span class="navbar-toggler-icon"></span>
      </button>

      <div class="collapse navbar-collapse" id="navbarSupportedContent">
        <ul class="navbar-nav mr-auto">
          <NavLink to={`/packages`}>
            Packages
            <span className="badge badge-secondary ml-2"><FontAwesomeIcon icon="cube" /> {local_packages_count}</span> 
            <span className="badge badge-secondary ml-2"><FontAwesomeIcon icon="network-wired" /> {remote_packages_count}</span>
          </NavLink>
          <NavLink to={`/packages/create`}>Create</NavLink>
          <NavLink to={`/peers`}>
            Peers
            <span class="badge badge-secondary ml-2"><FontAwesomeIcon icon="users" /> {peers_count}</span>
          </NavLink>
          <NavLink to={`/about`}>About</NavLink>
        </ul>
        <div className="my-2">
          <span className="badge badge-secondary p-2">
            <FontAwesomeIcon icon="angle-down" /> TBD MB/s 
          </span>
          <span className="badge badge-secondary ml-3 p-2">
            <FontAwesomeIcon icon="angle-up" /> TBD MB/s
          </span>
          <MyId className="small badge badge-info p-2 ml-3" />
        </div>
      </div>
    </nav>



    <div class="container-fluid mt-2">
      <ServerStatusHeader />
      <Tasks />
      <Routes />
    </div>

    <WebSocketHandler />
  </div>
)

const mapStateToProps = state => ({
  peers_count: state.Peers.peers_count,
  local_packages_count: state.Packages.local_packages_count,
  remote_packages_count: state.Packages.remote_packages_count
})

export default connect(mapStateToProps)(App);
