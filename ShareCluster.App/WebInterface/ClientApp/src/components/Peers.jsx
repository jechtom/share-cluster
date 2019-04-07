import React, { Component } from "react";
import axios from "axios";
import { connect } from 'react-redux'
import { FontAwesomeIcon } from '@fortawesome/react-fontawesome'

const Peers = ({ data }) => (
      <div>
        <h2>
          Peers <span class="badge badge-secondary"><FontAwesomeIcon icon="users" /> {data.peers_count}</span>
        </h2>
        <table class="table">
          <tbody>
            {data.peers.map(c => <tr key={c.Address}>
              <td><span class="badge badge-secondary p-2 mr-2"><FontAwesomeIcon icon="user" /> #{c.IdShort}</span> <code>{c.Endpoint}</code></td>
            </tr>)}
          </tbody>
        </table> 
        { data.peers.length == 0 && <div class="p-3 mb-2 bg-light text-dark">No remote peers found.</div> }
      </div>
)

const mapStateToProps = state => ({
  data: state.Peers
})

export default connect(mapStateToProps)(Peers);