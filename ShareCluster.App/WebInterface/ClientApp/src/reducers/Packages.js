export default function Packages(state = initialState(), action) {
    switch (action.type) {
      case 'PACKAGES_CHANGED':
        return doSearchOnState({ 
          ...state,
          groups: [],
          groups_all: action.data.Groups,
          local_packages_count: action.data.LocalPackages,
          remote_packages_count: action.data.RemotePackages
        });
      case 'PACKAGES_SEARCH_CHANGE':
        return doSearchOnState({
          ...state,
          search: applySearch(state.search, action.term)
        });
      case 'PACKAGES_SEARCH_RESET':
        return doSearchOnState({
          ...state,
          search: resetSearh()
        });
      default:
        return state;
    }
  }

const initialState = () => ({
  groups: [], 
  groups_all: [],
  local_packages_count: 0, 
  remote_packages_count: 0, 
  search: resetSearh()
})

const resetSearh = () => ({
  term: "",
  is_active: false,
  type: "unknown"
})

const applySearch = (search, term) => {
  if(term === "") return resetSearh();

  return {
    term: term,
    is_active: true,
    type: "unknown"
  }
}

const doSearchOnState = (state) => {
  
  // search disabled
  if(!state.search.is_active) return {
    ...state,
    groups: state.groups_all
  };

  // search - how?
  return {
    ...state,
    groups: []
  };
}