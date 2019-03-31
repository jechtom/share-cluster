import React from "react";
import PropTypes from "prop-types";
const Input = ({ type, id, value, handleChange, className }) => (
  <input
    type={type}
    className={ "form-control " + className }
    id={id}
    value={value}
    onChange={handleChange}
    required
  />
);
Input.propTypes = {
  type: PropTypes.string.isRequired,
  id: PropTypes.string.isRequired,
  value: PropTypes.string.isRequired,
  handleChange: PropTypes.func.isRequired
};
export default Input;