import axios from "axios";
import { uri_api } from '../constants'

// remark: replaces with service ApiClient and redux-thunk

const ApiServiceMiddleware = store => next => action => {
    next(action)
    switch (action.type) {
      case 'COMMAND_API':
        // {
        //   type == "COMMAND_API"
        //   operation == endpoint URL
        //   data == payload to post
        //   callback == action to invoke on server OK response 
        // }
        var url = uri_api + "/" + action.operation;
        console.log("Post to: " + url)
        console.log(action.data)
        axios
          .post(url, action.data)
            .then(response => {
              console.log(response);
              if(callback)
              {
                return next({
                  type: action.callback,
                  data: response
                });
              }
            })
            .catch(error => console.error(error));
        break
      default:
        break
    }
  }
  
  export default ApiServiceMiddleware;