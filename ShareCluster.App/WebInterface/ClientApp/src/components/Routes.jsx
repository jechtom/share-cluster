import React from "react";
import { HashRouter, Switch, Route, Link, Redirect } from 'react-router-dom';

import Packages from "./Packages.jsx";
import CreatePackage from "./CreatePackage.jsx";
import Peers from "./Peers.jsx";
import About from "./About.jsx";
import ExtractPackage from "./ExtractPackage.jsx";


export default () => (
    <Switch>
        <Redirect exact from='/' to='/packages' />
        <Route exact path='/packages/create' component={CreatePackage}/>
        <Route exact path='/packages/extract' component={ExtractPackage}/>
        <Route exact path='/packages' component={Packages}/>
        <Route exact path='/peers' component={Peers}/>
        <Route exact path='/about' component={About}/>
    </Switch>
)