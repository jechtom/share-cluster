import axios from "axios";
import { uri_api } from '../constants'

export default function Commands(state = null, action) {
    switch (action.type) {
      case 'COMMAND_API':
   
        axios
          .post(uri_api + "/" + action.operation, action.data)
          .then(response => console.log(response))
          .catch(error => console.log(error));

        return null;
      default:
        return state;
    }
  }