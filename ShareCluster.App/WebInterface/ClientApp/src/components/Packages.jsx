import React, { Component } from "react";
import { Link } from 'react-router-dom'
import BasicLink from './presentational/BasicLink.jsx'
import * as Commands from '../actions/commands'
import { connect } from 'react-redux'
import { FontAwesomeIcon } from '@fortawesome/react-fontawesome'

const Packages = ({ data, local_packages_count, remote_packages_count, package_delete, package_verify, package_download, package_download_stop }) => {

  const GridPackages = () => (
    <table class="table">
    <thead>
      <tr>
        <th>Created</th>
        <th>Name</th>
        <th>Size</th>
        <th>Remote availability</th>
        <th>Local copy</th>
        <th>Commands</th>
      </tr>
    </thead>
    {data.groups.map(g => <tbody key={g.GroupId}>
      {g.Packages.sort((i1, i2) => i1.CreatedSort > i2.CreatedSort).map(p => <tr key={p.Id}>
        <td>
          <FontAwesomeIcon icon="cube" /> { p.Name } <code class="small"><FontAwesomeIcon icon="hashtag" />{p.IdShort}</code>
        </td>
        <td>{ p.CreatedFormatted }</td>
        <td>{ p.SizeFormatted }</td>
        <td><FontAwesomeIcon icon="satellite-dish"/> { p.Seeders } seeders / { p.Leechers } leechers</td>
        <td><FontAwesomeIcon icon={ p.IsLocal ? "check" : "times" }/> { p.IsLocal ? "Downloaded" : "Not downloaded" }</td>
        <td>
          <BasicLink onClick={ (e) => package_delete(p.Id) } alt="Delete"><FontAwesomeIcon icon="trash-alt" /></BasicLink>
          <BasicLink onClick={ (e) => alert("N/A yet") } alt="Extract"><FontAwesomeIcon icon="box-open" /></BasicLink>
          <BasicLink onClick={ (e) => package_verify(p.Id) } alt="Verify"><FontAwesomeIcon icon="hat-wizard" /></BasicLink>
          <BasicLink onClick={ (e) => package_download(p.Id) } alt="Download"><FontAwesomeIcon icon="file-download" /></BasicLink>
          <BasicLink onClick={ (e) => package_download_stop(p.Id) } alt="Stop"><FontAwesomeIcon icon="stop-circle" /></BasicLink>
        </td>
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
  package_download_stop: id => dispatch(Commands.package_download_stop(id))
})

export default connect(mapStateToProps, mapDispatchToProps)(Packages);