import React, { Component } from "react";
import ReactDOM from "react-dom";
import FormContainer from "./FormContainer.jsx";
import PackagesMasterView from "./PackagesMasterView.jsx";
import { HashRouter, Switch, Route, Link } from 'react-router-dom';

class App extends Component {
  constructor() {
    super();
    this.state = {
    };
  }
  handleChange(event) {
    this.setState({ [event.target.id]: event.target.value });
  }
  render() {
    return (
      <HashRouter>
        <div>
          <nav class="navbar navbar-expand-lg navbar-light bg-light">
            <span class="navbar-brand">ShareCluster</span>
          </nav>

          <Link to={`/`}>Home</Link>
          <Link to={`/a`}>Aaaa</Link>

          <div class="container-fluid">
              <Switch>
                <Route exact path='/' component={FormContainer}/>
                <Route path='/a' component={PackagesMasterView}/>
              </Switch>
          </div>
        </div>
      </HashRouter>
    );
  }
}
export default App;

const wrapper = document.getElementById("app");
wrapper ? ReactDOM.render(<App />, wrapper) : false;
