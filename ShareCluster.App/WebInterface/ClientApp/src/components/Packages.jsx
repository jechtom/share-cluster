import React, { Component } from "react";
import { Link } from 'react-router-dom'
import BasicLink from './presentational/BasicLink.jsx'
import * as Commands from '../actions/commands'
import { connect } from 'react-redux'
import { FontAwesomeIcon } from '@fortawesome/react-fontawesome'

const Packages = ({ data, local_packages_count, remote_packages_count, package_delete, package_verify, package_download, package_download_stop, create_package_form_with_group }) => {

  const GridPackages = () => (
    <table class="table">
    <thead>
      <tr>
        <th></th>
        <th>Local copy</th>
        <th>Commands</th>
        <th>Remote availability</th>
        <th>Size</th>
        <th>Created</th>
      </tr>
    </thead>
    {data.groups.map(g => <tbody key={g.GroupId}>
      <tr>
        <td>
          Group <span class="badge badge-secondary"><FontAwesomeIcon icon="layer-group" /> #{g.GroupIdShort}</span>
        </td>
        <td>
        </td>
        <td>
          <BasicLink onClick={ (e) => create_package_form_with_group(g.GroupId, g.Name) } alt="Create new version" className="m-2"><FontAwesomeIcon icon="folder-plus" /> Create new package to this group</BasicLink>
        </td>
        <td colSpan="3">
        </td>
      </tr>
      {g.Packages.map(p => <tr key={p.Id} className={ p.IsDownloaded ? "table-success" : p.IsDownloading ? "table-warning" : "table-secondary" }>
        <td className="pl-3">
          Package <span class="badge badge-primary p-2 m-1"><FontAwesomeIcon icon="cube" /> #{p.IdShort}</span> { p.Name }
        </td>
        <td><FontAwesomeIcon icon={ p.IsDownloaded ? "check" : p.IsDownloading ? "angle-double-down" : "times" }/> { p.IsDownloaded ? "Downloaded" : p.IsDownloading ? "Downloading..." : "Not downloaded" }</td>
        <td>
          { p.IsLocal && <BasicLink onClick={ (e) => package_delete(p.Id) } alt="Delete" className="m-2"><FontAwesomeIcon icon="trash-alt" /> Delete</BasicLink>}
          { p.IsDownloaded && <BasicLink onClick={ (e) => alert("N/A yet") } alt="Extract" className="m-2"><FontAwesomeIcon icon="box-open" /> Extract</BasicLink>}
          { p.IsDownloaded && <BasicLink onClick={ (e) => package_verify(p.Id) } alt="Verify" className="m-2"><FontAwesomeIcon icon="hat-wizard" /> Verify</BasicLink>}
          { p.IsDownloading && <BasicLink onClick={ (e) => package_download_stop(p.Id) } alt="Stop" className="m-2"><FontAwesomeIcon icon="stop-circle" /> Stop</BasicLink>}
          { !p.IsLocal && <BasicLink onClick={ (e) => package_download(p.Id) } alt="Download" className="m-2"><FontAwesomeIcon icon="arrow-alt-circle-down" /> Download</BasicLink>}
        </td>
        <td><FontAwesomeIcon icon="satellite-dish"/> { p.Seeders } seeders / { p.Leechers } leechers</td>
        <td>{ p.SizeFormatted }</td>
        <td>{ p.CreatedFormatted }</td>
      </tr>)}
    </tbody>)}
  </table> 
  )

  const RenderGrid = () => {
    if(data.groups.length > 0) return GridPackages();
    return (<div class="p-3 mb-2 bg-light text-dark">No packages found. You can start with <Link to='/packages/create'><FontAwesomeIcon icon="folder-plus" /> creating one</Link>.</div>);
  }

  return (
    <div>
      <div>
        <div className="btn-group float-lg-right">
          <Link className="btn btn-primary" to={"/packages/create"}><FontAwesomeIcon icon="folder-plus" /> Create new package</Link>
        </div>
        <h2>
          <FontAwesomeIcon icon="cubes" /> Packages <span className="badge badge-secondary">
            <FontAwesomeIcon icon="cube" /> {local_packages_count}
          </span> <span className="badge badge-secondary ml-2">
            <FontAwesomeIcon icon="network-wired" /> {remote_packages_count}
          </span>
        </h2>
      </div>
      <RenderGrid />
    </div>
  )
}

const mapStateToProps = state => ({
  data: state.Packages,
  local_packages_count: state.Packages.local_packages_count,
  remote_packages_count: state.Packages.remote_packages_count
})

const mapDispatchToProps = dispatch => ({
  package_delete: id => dispatch(Commands.package_delete(id)),
  package_verify: id => dispatch(Commands.package_verify(id)),
  package_download: id => dispatch(Commands.package_download(id)),
  package_download_stop: id => dispatch(Commands.package_download_stop(id)),
  create_package_form_with_group: (groupId,name) => dispatch(Commands.create_package_form_with_group(groupId,name))
})

export default connect(mapStateToProps, mapDispatchToProps)(Packages);