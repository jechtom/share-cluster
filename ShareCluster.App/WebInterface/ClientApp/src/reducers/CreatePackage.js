import axios from "axios";
import { uri_api } from '../constants'

export default function CreatePackage(state = applyCustomRules(createInitialState(), "", false), action) {
    switch (action.type) {
      case 'CREATE_PACKAGE_FORM_CHANGE':
        return applyCustomRules({ ...state, [action.id]: action.value }, action.id, action.value);
      case 'CREATE_PACKAGE_FORM_OK':
        console.log("OK");
        return state;
      default:
        return state;
    }
  }

function applyCustomRules(state, id, value) {
  if(!state.name_custom) {
    state.name = state.path
      .replace(/^.+[/\\](?=.)/i, "")
      .replace(/[/\\]$/i,"");
  }

  state.type_is_archive = false;
  state.type_is_reference = true;

  if(id.match("/^type_/g") && value)
  {
    //state.type_is_archive = id === "type_is_archive";
    //state.type_is_reference = id === "type_is_reference";
  }
  
  return state;
}

function createInitialState() {
  return { 
    name: '', 
    path: '',
    name_custom: false,
    package_type: "archive"
  };
}