import React, { Component } from "react";
import axios from "axios";
import { connect } from 'react-redux'
import { FontAwesomeIcon } from '@fortawesome/react-fontawesome'

const Peers = ({ data }) => (
      <div>
        <h1><FontAwesomeIcon icon="users" /> Peers</h1>
        { data.peers.length == 0 && <div>No peers found.</div> }
        <table class="table">
          <tbody>
            {data.peers.map(c => <tr key={c.Address}>
              <td><FontAwesomeIcon icon="user" /> <code>{c.Address}</code></td>
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
  data: state.Peers
})

export default connect(mapStateToProps)(Peers);