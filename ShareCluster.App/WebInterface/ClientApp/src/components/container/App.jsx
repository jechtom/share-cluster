import React, { Component } from "react";
import ReactDOM from "react-dom";
import FormContainer from "./FormContainer.jsx";
import PackagesMasterView from "./PackagesMasterView.jsx";

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
      <div>

        <nav class="navbar navbar-expand-lg navbar-light bg-light">
          <span class="navbar-brand">ShareCluster</span>
        </nav>

        <div class="container-fluid">
          <FormContainer />
        </div>

        <div class="container-fluid">
          <PackagesMasterView />
        </div>
        
      </div>
    );
  }
}
export default App;

const wrapper = document.getElementById("app");
wrapper ? ReactDOM.render(<App />, wrapper) : false;
