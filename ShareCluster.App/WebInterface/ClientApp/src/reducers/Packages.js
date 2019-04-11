export default function Packages(state = initialState(), action) {
    switch (action.type) {
      case 'PACKAGES_CHANGED':
        return doSearchOnState({ 
          ...state,
          groups: [],
          groups_all: action.data.Groups,
          local_packages_count: action.data.LocalPackages,
          remote_packages_count: action.data.RemotePackages,
          total_local_size_formatted: action.data.TotalLocalSizeFormatted
        });
      case 'PACKAGES_CHANGED_PARTIAL':
        if(action.data.Groups.length == 0) return state;
        
        var newGroups =  Array.from(state.groups_all);
        action.data.Groups.forEach(ev => {

          for(var gi = 0; gi < newGroups.length; gi += 1) {
            if(newGroups[gi].GroupId === ev.GroupId) {
              newGroups[gi] = ev;
            }
          }
        });  

        return doSearchOnState({
          ...state,
          groups_all: newGroups
        });
      case 'PACKAGES_SEARCH_CHANGE':
        return doSearchOnState({
          ...state,
          search: applySearch(state.search, action.term)
        });
      case 'PACKAGES_SEARCH_RESET':
        return doSearchOnState({
          ...state,
          search: resetSearch()
        });
      case 'PACKAGES_DELETE_MODAL':
        return {
          ...state,
          delete_package_dialog: {
            visible: true,
            package_name: action.package_name,
            package_id: action.package_id
          }
        };
      case 'PACKAGE_DELETE_CANCEL':
        return {
          ...state,
          delete_package_dialog: cancelDeletePackageDialog()
        };
      default:
        return state;
    }
  }

const initialState = () => ({
  groups: [], 
  groups_all: [],
  local_packages_count: 0, 
  remote_packages_count: 0, 
  total_local_size_formatted: "",
  search: resetSearch(),
  delete_package_dialog: cancelDeletePackageDialog()
})

const resetSearch = () => ({
  term: "",
  is_active: false,
  type: "unknown"
})

const cancelDeletePackageDialog = () => ({
  visible: false,
  package_name: null,
  package_id: null
});

const applySearch = (search, term) => {
  if(term === "") return resetSearch();

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