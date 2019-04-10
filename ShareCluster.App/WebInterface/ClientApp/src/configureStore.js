import { createStore, applyMiddleware, compose } from 'redux'
import thunk from 'redux-thunk';
import createRootReducer from './reducers'
import { createHashHistory } from 'history'
import { routerMiddleware } from 'connected-react-router'

export const history = createHashHistory()

export default function configureStoreAndHistory() {
  const store = createStore(
    createRootReducer(history),
    compose(
      applyMiddleware(
        routerMiddleware(history), 
        thunk
      )
    )
  );

  return { store, history };
}