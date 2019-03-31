import React, { Component } from "react";
import Link from './presentational/Link.jsx'
import * as Commands from '../actions/commands'
import { connect } from 'react-redux'
import { FontAwesomeIcon } from '@fortawesome/react-fontawesome'

const Packages = ({ data, local_packages_count, remote_packages_count, package_delete, package_verify, package_download, package_download_stop }) => (
    <div>
      <h1>
        <FontAwesomeIcon icon="cubes" /> Packages <span className="badge badge-secondary">
          <FontAwesomeIcon icon="cube" /> {local_packages_count}
        </span> <span className="badge badge-secondary ml-2">
          <FontAwesomeIcon icon="network-wired" /> {remote_packages_count}
        </span>
      </h1>
      <table class="table">
        {data.groups.map(g => <tbody key={g.GroupId}>
          {g.Packages.sort((i1, i2) => i1.CreatedSort > i2.CreatedSort).map(p => <tr key={p.Id}>
            <td><FontAwesomeIcon icon="cube" /> <code class="small">{g.GroupIdShort}/{p.IdShort}</code></td>
            <td>{ p.CreatedFormatted }</td>
            <td>{ p.KnownNames }</td>
            <td>{ p.SizeFormatted }</td>
            <td><FontAwesomeIcon icon="cloud-download-alt"/> { p.Leechers } / <FontAwesomeIcon icon="cloud-upload-alt"/> { p.Seeders }</td>
            <td>
              <Link onClick={ (e) => package_delete(p.Id) } alt="Delete"><FontAwesomeIcon icon="trash-alt" /></Link>
              <Link onClick={ (e) => alert("N/A yet") } alt="Extract"><FontAwesomeIcon icon="box-open" /></Link>
              <Link onClick={ (e) => package_verify(p.Id) } alt="Verify"><FontAwesomeIcon icon="hat-wizard" /></Link>
              <Link onClick={ (e) => package_download(p.Id) } alt="Download"><FontAwesomeIcon icon="file-download" /></Link>
              <Link onClick={ (e) => package_download_stop(p.Id) } alt="Stop"><FontAwesomeIcon icon="stop-circle" /></Link>
            </td>
          </tr>)}
        </tbody>)}
      </table> 
    </div>
)


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