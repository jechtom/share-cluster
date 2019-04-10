import { createStore, applyMiddleware, compose } from 'redux'
import thunk from 'redux-thunk';
import createRootReducer from './reducers'
import { createHashHistory } from 'history'
import { routerMiddleware } from 'connected-react-router'
import RedirectMiddleware from './middlewares/RedirectMiddleware'

export const history = createHashHistory()

export default function configureStoreAndHistory() {
  const store = createStore(
    createRootReducer(history),
    compose(
      applyMiddleware(
        RedirectMiddleware,
        thunk,
        routerMiddleware(history)
      )
    )
  );

  return { store, history };
}