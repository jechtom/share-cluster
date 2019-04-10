import 'bootstrap';
import './styles/app.scss';
import App from "./components/App.jsx";

import React from 'react';
import { render } from 'react-dom'
import { createStore, applyMiddleware } from 'redux'
import ApiServiceMiddleware from './middlewares/ApiServiceMiddleware';
import { Provider } from 'react-redux'
import thunk from 'redux-thunk';
import createRootReducer from './reducers'
import { createHashHistory } from 'history'
import { routerMiddleware, ConnectedRouter } from 'connected-react-router'

import './fontawesome-loader'

const store = configureStore(null /* provide initial state if any */)

render(
  <Provider store={store}>
    <ConnectedRouter history={history}>
      <App />
    </ConnectedRouter>
  </Provider>,
  document.getElementById('root')
)