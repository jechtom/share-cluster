export default function Tasks(state = {tasks : [], tasks_any: false}, action) {
    switch (action.type) {
      case 'TASKS_CHANGED':
        return { tasks : action.data.Tasks, tasks_any: action.data.Tasks.length > 0 }
      default:
        return state;
    }
  }