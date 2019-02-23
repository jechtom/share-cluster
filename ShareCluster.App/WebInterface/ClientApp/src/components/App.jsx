import React from "react";
import { HashRouter, Switch, Route, Link, Redirect } from 'react-router-dom';
import FormContainer from "./FormContainer.jsx";
import Packages from "./Packages.jsx";
import Peers from "./Peers.jsx";
import WebSocketHandler from '../containers/WebSocketHandler.jsx';
import ServerStatus from '../containers/ServerStatus.jsx';
import { FontAwesomeIcon } from '@fortawesome/react-fontawesome'
import NavLink from "./NavLink.jsx";

const App = () => (
  <HashRouter>
    <div>

      <nav class="navbar navbar-expand-lg navbar-light bg-light">
        <a class="navbar-brand" href="https://github.com/jechtom/share-cluster" target="_blank">
          <FontAwesomeIcon icon="cubes" /> ShareCluster
        </a>
        <button class="navbar-toggler" type="button" data-toggle="collapse" data-target="#navbarSupportedContent" aria-controls="navbarSupportedContent" aria-expanded="false" aria-label="Toggle navigation">
          <span class="navbar-toggler-icon"></span>
        </button>

        <div class="collapse navbar-collapse" id="navbarSupportedContent">
          <ul class="navbar-nav mr-auto">
            <NavLink to={`/packages`}>Packages</NavLink>
            <NavLink to={`/peers`}>Peers</NavLink>
          </ul>
        </div>
      </nav>

      <ServerStatus />


      <div class="container-fluid">
        <Switch>
          <Redirect exact from='/' to='/packages' />
          <Route path='/packages' component={Packages}/>
          <Route path='/peers' component={Peers}/>
        </Switch>
      </div>

      <WebSocketHandler />
    </div>
  </HashRouter>
)

export default App;
