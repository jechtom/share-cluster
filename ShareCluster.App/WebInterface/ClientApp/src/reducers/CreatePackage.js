export default function CreatePackage(state = createInitialState(null, null), action) {
    switch (action.type) {
      case 'CREATE_PACKAGE_FORM_CHANGE':
        return applyCustomRules({ ...state, [action.id]: action.value });
      case 'CREATE_PACKAGE_FORM_WITH_GROUP':
        return applyCustomRules({ 
          ...state, 
          group_use: true, 
          group_name: action.group_name, 
          group_id: action.group_id, 
          name_custom: true, // use original name from package as default (but it can be changed)
          name: action.group_name 
        });
      case 'CREATE_PACKAGE_FORM_WITHOUT_GROUP':
        return applyCustomRules({ 
          ...state, 
          group_use: false, 
          name_custom: false 
        });
      case 'CREATE_PACKAGE_FORM_OK':
        console.log("Package create request has been accepted");
        return createInitialState();
      case 'CREATE_PACKAGE_FORM_SUBMITTING':
        return applyCustomRules({ 
          ...state, 
          is_sending: true,
          error_message: null
        });
      case 'CREATE_PACKAGE_FORM_ERROR':
        return applyCustomRules({ 
          ...state, 
          is_sending: false,
          error_message: action.message
        });
      default:
        return state;
    }
  }

function applyCustomRules(state) {
  // get default name
  if(!state.name_custom) {
    state.name = state.path
      .replace(/^.+[/\\](?=.)/i, "")
      .replace(/[/\\]$/i,"");
  }
  return state;
}

const createInitialState = () =>
   applyCustomRules({ 
    group_use: false,
    group_id: null,
    group_name: null,
    name: '', 
    path: '',
    name_custom: false,
    package_type: "archive",
    is_sending: false,
    error_message: null
  })