import React, { Component } from "react";
import axios from "axios";
import ReactDOM from "react-dom";
import Input from "../presentational/Input.jsx";
class PackagesMasterView extends Component {
  constructor() {
    super();
    this.state = {
      packages: []
    };
  }
 
  render() {
    
      return (
      <div>
        <h1>Hello</h1>
        <table class="table">
          <tbody>
            {this.state.packages.map(c => <tr key={c.id}>
              <td>{c.name}</td>
              <td>{c.sizeFormatted}</td>
            </tr>)}
          </tbody>
        </table> 
      </div>
    );
  }



  componentDidMount() {
    axios
      .get("http://localhost:13978/test")
      .then(response => {
        console.log(response);
        // create an array of contacts only with relevant data
        const newPackages = response.data.map(c => {
          return c;
        });

        // create a new "State" object without mutating 
        // the original State object. 
        const newState = Object.assign({}, this.state, {
          packages: newPackages
        });

        // store the new state object in the component's state
        this.setState(newState);
      })
      .catch(error => console.log(error));
  }
}
export default PackagesMasterView;