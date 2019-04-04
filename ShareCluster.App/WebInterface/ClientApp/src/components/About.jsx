import React from "react";
import { uri_project_home } from '../constants'
import { FontAwesomeIcon } from '@fortawesome/react-fontawesome'

const Peers = () => (
      <div>
        <h2>
          <FontAwesomeIcon icon="question-circle" /> About
        </h2>
        <p><a href={uri_project_home} target="_blank"><FontAwesomeIcon icon="external-link-alt" /> Project GitHub</a></p>
      </div>
)

export default Peers;