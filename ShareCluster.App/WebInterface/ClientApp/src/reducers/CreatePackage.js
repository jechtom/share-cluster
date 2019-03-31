export default function CreatePackage(state = applyCustomRules({ 
  name: 'new package name', 
  path: 'c:\\aaaaaa123',
  name_custom: false
}), action) {
    switch (action.type) {
      case 'CP_FORM_CHANGE':
        return applyCustomRules({ ...state, [action.id]: action.value });
      default:
        return state;
    }
  }

function applyCustomRules(state) {
  if(!state.name_custom) {
    state.name = state.path
      .replace(/^.+[/\\](?=.)/i, "")
      .replace(/[/\\]$/i,"");
  }
  return state;
}