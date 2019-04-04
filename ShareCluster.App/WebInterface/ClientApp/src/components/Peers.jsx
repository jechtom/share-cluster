import React, { Component } from "react";
import axios from "axios";
import { connect } from 'react-redux'
import { FontAwesomeIcon } from '@fortawesome/react-fontawesome'

const Peers = ({ data }) => (
      <div>
        <h2>
          Peers <span class="badge badge-secondary"><FontAwesomeIcon icon="users" /> {data.peers_count}</span>
        </h2>
        { data.peers.length == 0 && <div class="p-3 mb-2 bg-light text-dark">No peers found.</div> }
        <table class="table">
          <tbody>
            {data.peers.map(c => <tr key={c.Address}>
              <td><FontAwesomeIcon icon="user" /> <code>{c.Address}</code></td>
            </tr>)}
          </tbody>
        </table> 
      </div>
)

const mapStateToProps = state => ({
  data: state.Peers
})

export default connect(mapStateToProps)(Peers);