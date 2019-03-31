import React from "react";
import { HashRouter, Switch, Route, Link, Redirect } from 'react-router-dom';
import Packages from "./Packages.jsx";
import CreatePackage from "./CreatePackage.jsx";
import Peers from "./Peers.jsx";
import WebSocketHandler from '../containers/WebSocketHandler.jsx';
import ServerStatus from '../containers/ServerStatus.jsx';
import { FontAwesomeIcon } from '@fortawesome/react-fontawesome'
import NavLink from "./NavLink.jsx";
import { connect } from 'react-redux'

const App = ({ peers_count, local_packages_count, remote_packages_count }) => (
  <HashRouter>
    <div>

      <nav class="navbar navbar-expand-lg navbar-light bg-light">
        <a class="navbar-brand" href="https://github.com/jechtom/share-cluster" target="_blank">
          <FontAwesomeIcon icon="th-large" /> ShareCluster 
          <small className="ml-2">
            <ServerStatus />
          </small>
        </a>
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
            <NavLink to={`/peers`}>
              Peers
              <span class="badge badge-secondary ml-2"><FontAwesomeIcon icon="users" /> {peers_count}</span>
            </NavLink>
            {/* <li className="nav-item">
              <a className="nav-link" href="https://github.com/jechtom/share-cluster">
                <FontAwesomeIcon icon="external-link-alt" /> Docs
              </a>
            </li> */}
          </ul>
          <div className="my-2">
            <span className="badge badge-secondary p-2">
              <FontAwesomeIcon icon="angle-down" /> TBD MB/s 
            </span>
            <span className="badge badge-secondary ml-3 p-2">
              <FontAwesomeIcon icon="angle-up" /> TBD MB/s
            </span>
          </div>
        </div>
      </nav>



      <div class="container-fluid">
        <Switch>
          <Redirect exact from='/' to='/packages' />
          <Route path='/packages/create' component={CreatePackage}/>
          <Route path='/packages' component={Packages}/>
          <Route path='/peers' component={Peers}/>
        </Switch>
      </div>

      <WebSocketHandler />
    </div>
  </HashRouter>
)

const mapStateToProps = state => ({
  peers_count: state.Peers.peers_count,
  local_packages_count: state.Packages.local_packages_count,
  remote_packages_count: state.Packages.remote_packages_count
})

export default connect(mapStateToProps)(App);
