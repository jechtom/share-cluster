import React, { Component } from "react";
import axios from "axios";
import { connect } from 'react-redux'
import { FontAwesomeIcon } from '@fortawesome/react-fontawesome'

const Packages = ({ data }) => (
      <div>
        <h1><FontAwesomeIcon icon="cubes" /> Packages</h1>
        <table class="table">
          <tbody>
            {data.packages.map(c => <tr key={c.Id}>
              <td><FontAwesomeIcon icon="cube" /> {c.KnownNames} <code class="small">{c.IdShort}</code></td>
              <td>{c.SizeFormatted}</td>
            </tr>)}
          </tbody>
        </table> 
      </div>
)


  // componentDidMount() {
  //   axios
  //     .get("http://localhost:13978/test")
  //     .then(response => {
  //       console.log(response);
  //       // create an array of contacts only with relevant data
  //       const newPackages = response.data.map(c => {
  //         return c;
  //       });

  //       // create a new "State" object without mutating 
  //       // the original State object. 
  //       const newState = Object.assign({}, this.state, {
  //         packages: newPackages
  //       });

  //       // store the new state object in the component's state
  //       this.setState(newState);
  //     })
  //     .catch(error => console.log(error));
  // }

const mapStateToProps = state => ({
  data: state.Packages
})

export default connect(mapStateToProps)(Packages);