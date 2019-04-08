import React, { Component } from "react";
import { Link } from 'react-router-dom'
import BasicLink from './presentational/BasicLink.jsx'
import * as Commands from '../actions/commands'
import { connect } from 'react-redux'
import { FontAwesomeIcon } from '@fortawesome/react-fontawesome'

const Tasks = ({ any, any_to_dismiss, tasks, dismiss_all }) => {

  function ResolveAlertClass(t) {
    if(t.IsRunning) return "primary"
    if(t.IsSuccess) return "success"
    return "danger"
  }

  function ResolveIcon(t) {
    if(t.IsRunning) return "hourglass"
    if(t.IsSuccess) return "check-circle"
    return "exclamation-triangle"
  }
  
  function ResolveState(t) {
    if(t.IsRunning) {
      return "Running for " + t.DurationText + "...";
    } 
    if(t.IsSuccess) return "Completed successfully in " + t.DurationText
    return "Failed"
  }

  const RenderTasks = () => (
    <div>
      <div class="clearfix mb-2">
        {tasks.map(t => <div key={t.Id} className={"alert alert-" + ResolveAlertClass(t)}>
          {t.Title}<br />
          <FontAwesomeIcon icon={ResolveIcon(t)} className="mr-1" /> <strong>{ResolveState(t)}</strong> { t.MeasureText && <span>{ t.MeasureText }</span> }
          {/* { t.IsRunning || <button type="button" class="close" aria-label="Close" onClick={dismiss_all}><span aria-hidden="true">&times;</span></button> } */}
        </div>)}
        { any_to_dismiss && <button onClick={dismiss_all} className="btn btn-secondary float-left">&times; Dismiss all completed</button>}
      </div>
      <hr />
    </div>
  )

  if(any) { 
    return RenderTasks();
  } else {
    return "";
  }
}

const mapStateToProps = state => ({
  tasks: state.Tasks.tasks,
  any: state.Tasks.tasks.length > 0,
  any_to_dismiss: state.Tasks.tasks.some(t => !t.IsRunning)
})

const mapDispatchToProps = dispatch => ({
  dismiss_all: () => dispatch(Commands.tasks_dismiss_all())
})

export default connect(mapStateToProps, mapDispatchToProps)(Tasks);