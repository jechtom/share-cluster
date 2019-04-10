import { createStore, applyMiddleware } from 'redux'
import thunk from 'redux-thunk';
import createRootReducer from './reducers'
import { createHashHistory } from 'history'
import { routerMiddleware } from 'connected-react-router'

export const history = createHashHistory()

export default function configureStore(preloadState) {
  const store = createStore(
    createRootReducer(history),
    preloadState,
    compose(
      applyMiddleware(
        routerMiddleware(history), 
        thunk
      )
    )
  );

  return store;
}