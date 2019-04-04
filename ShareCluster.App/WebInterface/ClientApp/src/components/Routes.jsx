import React from "react";
import { HashRouter, Switch, Route, Link, Redirect } from 'react-router-dom';

import Packages from "./Packages.jsx";
import CreatePackage from "./CreatePackage.jsx";
import Peers from "./Peers.jsx";
import About from "./About.jsx";

export default () => (
    <Switch>
        <Redirect exact from='/' to='/packages' />
        <Route path='/packages/create' component={CreatePackage}/>
        <Route path='/packages' component={Packages}/>
        <Route path='/peers' component={Peers}/>
        <Route path='/about' component={About}/>
    </Switch>
)