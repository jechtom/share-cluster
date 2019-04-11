import React, { Component } from "react";
import { Link } from 'react-router-dom'
import BasicLink from './presentational/BasicLink.jsx'
import * as Commands from '../actions/commands'
import { connect } from 'react-redux'
import { FontAwesomeIcon } from '@fortawesome/react-fontawesome'
import CopyLink from './presentational/CopyLink.jsx'
import PackagesSearch from './PackagesSearch.jsx'
import PackagesModals from './PackagesModals.jsx'

const Packages = ({ 
  data, 
  handle_search_reset, 
  local_packages_count, remote_packages_count, total_local_size_formatted, 
  package_delete, package_verify, package_download, package_download_stop, package_extract, 
  create_package_form_with_group, create_package_form_without_group }) => {

  const GridPackages = () => (
    <div>
      <table class="table table-borderless table-sm">
      <thead>
        <tr>
          <th>Name</th>
          <th>Commands</th>
          <th>Local copy</th>
          <th>Availability</th>
          <th>Size</th>
          <th>Created</th>
        </tr>
      </thead>
      {data.groups.map(g => <tbody key={g.GroupId}>
        {g.Packages.map((p,index) => <tr key={p.Id} className={ p.IsDownloaded ? "table-success" : p.IsDownloading ? "table-warning" : "table-secondary" }>
          <td className={ " pl-4 " + ((index == 0) ? "table-group-top" : "table-group-between")}>
            <CopyLink className="badge badge-primary p-1 m-1" icon="cube" textCopy={"package#" + p.Id} text={ "#" + p.IdShort} /> {p.Name}
          </td>
          <td>
            { p.IsDownloaded && <BasicLink onClick={ (e) => package_extract(p.Id, p.Name, p.SizeFormatted) } alt="Extract" className="m-1"><FontAwesomeIcon icon="box-open" /> Extract</BasicLink>}
            { p.IsDownloaded && <BasicLink onClick={ (e) => package_verify(p.Id) } alt="Verify" className="m-1"><FontAwesomeIcon icon="bug" /> Verify</BasicLink>}
            { p.IsLocal && <BasicLink onClick={ (e) => package_delete(p.Id, p.Name) } alt="Delete" className="m-1"><FontAwesomeIcon icon="trash-alt" /> Delete</BasicLink>}
            { p.IsDownloading && <BasicLink onClick={ (e) => package_download_stop(p.Id) } alt="Stop" className="m-1"><FontAwesomeIcon icon="stop-circle" /> Stop</BasicLink>}
            { (!p.IsLocal || p.IsDownloadingPaused) && <BasicLink onClick={ (e) => package_download(p.Id) } alt="Download" className="m-1"><FontAwesomeIcon icon="arrow-alt-circle-down" /> Download</BasicLink>}
          </td>
          <td>
            <FontAwesomeIcon icon={ p.IsDownloadingPaused ? "pause" : p.IsDownloaded ? "check" : p.IsDownloading ? "angle-double-down" : "times" }/> { p.IsDownloadingPaused ? "Download paused" : p.IsDownloaded ? "Downloaded" : p.IsDownloading ? "Downloading..." : "Not downloaded" }
            { p.IsDownloading && p.Progress != null && <div class="progress">
              <div className="progress-bar progress-bar-striped progress-bar-animated bg-success" role="progressbar" style={{width: p.Progress.ProgressPercent + "%"}} aria-valuenow={p.Progress.ProgressPercent} aria-valuemin="0" aria-valuemax="100">{p.Progress.ProgressFormatted} {p.Progress.DownloadSpeedFormatted}</div>
            </div>}
            { p.IsDownloadingPaused && p.Progress != null && <div class="progress">
              <div className="progress-bar progress-bar-striped bg-warning" role="progressbar" style={{width: p.Progress.ProgressPercent + "%"}} aria-valuenow={p.Progress.ProgressPercent} aria-valuemin="0" aria-valuemax="100">{p.Progress.ProgressFormatted}</div>
            </div>}
          </td>
          <td><FontAwesomeIcon icon="satellite-dish"/> { p.Seeders + (p.IsDownloaded ? 1 : 0) } seeders / { p.Leechers + (p.IsDownloading ? 1 : 0) } leechers</td>
          <td>{ p.SizeFormatted }</td>
          <td>{ p.CreatedFormatted }</td>
        </tr>)}
        <tr>
          <td className="table-group-bottom">
            {/* Group <span class="badge badge-secondary"><FontAwesomeIcon icon="layer-group" /> #{g.GroupIdShort}</span> */}
            <BasicLink onClick={ (e) => create_package_form_with_group(g.GroupId, g.Name) } className="pl-3 m-1"><FontAwesomeIcon icon="folder-plus" /> Create new version</BasicLink>
          </td>
          <td>
          </td>
          <td>
          </td>
          <td colSpan="3">
          </td>
        </tr>
      </tbody>)}
    </table>
    <BasicLink onClick={ (e) => create_package_form_without_group() } className="pl-3 m-1"><FontAwesomeIcon icon="folder-plus" /> Create new package</BasicLink>
  </div> 
  )

  const RenderGrid = () => {
    if(data.groups.length > 0) return GridPackages();
    if(data.groups_all.length > 0) return (<div class="p-3 mb-2 bg-light text-dark"><FontAwesomeIcon icon="frown" /> Nothing found. TODO Sorry, search is not working yet... <BasicLink onClick={ (e) => handle_search_reset() }>Cancel search?</BasicLink></div>)
    return (<div class="p-3 mb-2 bg-light text-dark">No packages found. You can start with <Link to='/packages/create'><FontAwesomeIcon icon="folder-plus" /> creating one</Link>.</div>);
  }

  return (
    <div>
      <div class="clearfix">
        <h2 className="float-md-left">
          <FontAwesomeIcon icon="cubes" /> Packages <span className="badge badge-secondary">
            <FontAwesomeIcon icon="cube" /> {local_packages_count}
          </span> <span className="badge badge-secondary ml-2">
            <FontAwesomeIcon icon="network-wired" /> {remote_packages_count}
          </span> <span className="badge badge-secondary ml-2">
            <FontAwesomeIcon icon="hdd" /> { total_local_size_formatted }
          </span>
        </h2>

        <div className="float-md-right">
          <PackagesSearch />
        </div>
            
        {/* <div className="float-md-right">
          <div className="btn-group">
            <Link className="btn btn-primary" to={"/packages/create"}><FontAwesomeIcon icon="folder-plus" /> Create new package</Link>
          </div>
        </div> */}
      </div>

      <RenderGrid />

      <PackagesModals />
    </div>
  )
}

const mapStateToProps = state => ({
  data: state.Packages,
  search: state.Packages.search,
  local_packages_count: state.Packages.local_packages_count,
  remote_packages_count: state.Packages.remote_packages_count,
  total_local_size_formatted: state.Packages.total_local_size_formatted
})

const mapDispatchToProps = dispatch => ({
  handle_search_reset: () => dispatch(Commands.packages_search_reset()),
  package_delete: (id, name) => dispatch(Commands.packages_delete_modal(id,name)),
  package_extract: (id, name, size) => dispatch(Commands.extract_package_form(id, name, size)),
  package_verify: id => dispatch(Commands.package_verify(id)),
  package_download: id => dispatch(Commands.package_download(id)),
  package_download_stop: id => dispatch(Commands.package_download_stop(id)),
  create_package_form_with_group: (groupId,name) => dispatch(Commands.create_package_form_with_group(groupId,name)),
  create_package_form_without_group: () => dispatch(Commands.create_package_form_without_group()),
})

export default connect(mapStateToProps, mapDispatchToProps)(Packages);