import React, { Component } from "react";
import ReactDOM from "react-dom";
import Input from "../presentational/Input.jsx";
import Websocket from 'react-websocket';

class FormContainer extends Component {
  constructor() {
    super();
    this.state = {
      seo_title: "",
      is_connected: false
    };
    this.handleChange = this.handleChange.bind(this);
  }
  handleChange(event) {
    this.setState({ [event.target.id]: event.target.value });
  }

  handleData(data) {
    console.log(data);
    this.setState({ seo_title: data });
  }

  handleOpen() { this.setState({ is_connected: true } ); }
  handleClose() { this.setState({ is_connected: false } ); }

  render() {
    const { seo_title } = this.state;
    const classNameForConnect = "alert " + (this.state.is_connected ? "alert-success" : "alert-warning");
    return (
      <div>
        <h1>Hello</h1>
        <div className={ classNameForConnect }>{ this.state.is_connected ? "Connected" : "Disconnected" }</div>
        <strong>{seo_title}</strong>
        <form id="article-form">
          <Input
            text="SEO title"
            label="seo_title"
            type="text"
            id="seo_title"
            value={seo_title}
            handleChange={this.handleChange}
          />
        </form>

        <Websocket url='ws://localhost:13978/ws'
              onMessage={this.handleData.bind(this)}
              onOpen={this.handleOpen.bind(this)}
              onClose={this.handleClose.bind(this)}
              />

      </div>
    );
  }
}
export default FormContainer;