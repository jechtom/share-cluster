export default function Packages(state = {groups : [], local_packages_count: 0, remote_packages_count: 0 }, action) {
    switch (action.type) {
      case 'PACKAGES_CHANGED':
        return { 
          groups: action.data.Groups,
          local_packages_count: action.data.LocalPackages,
          remote_packages_count: action.data.RemotePackages 
        };
      default:
        return state;
    }
  }