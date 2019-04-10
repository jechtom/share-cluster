import 'bootstrap';
import './styles/app.scss';
import App from "./components/App.jsx";
import configureStoreAndHistory from "./configureStore";

import React from 'react';
import { render } from 'react-dom'
import { Provider } from 'react-redux'
import { ConnectedRouter } from 'connected-react-router'

import './fontawesome-loader'

const { store, history} = configureStoreAndHistory()

render(
  <Provider store={store}>
    <ConnectedRouter history={history}>
      <App />
    </ConnectedRouter>
  </Provider>,
  document.getElementById('root')
)