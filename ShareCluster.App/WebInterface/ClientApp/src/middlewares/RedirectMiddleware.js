// not needed - can be deleted

import { push } from 'connected-react-router'


const RedirectMiddleware = store => next => action => {
    next(action)
    switch (action.type) {
      case 'REDIRECT':
        var url = action.url;
        console.log("Redirect to: " + url)
        next(push(url));
        break
      default:
        break
    }
  }
  
  export default RedirectMiddleware;