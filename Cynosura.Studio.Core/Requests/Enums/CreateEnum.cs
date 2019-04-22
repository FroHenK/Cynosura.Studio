﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Cynosura.Studio.Core.Infrastructure;
using Cynosura.Studio.Core.Requests.EnumValues;
using MediatR;

namespace Cynosura.Studio.Core.Requests.Enums
{
    public class CreateEnum : IRequest<CreatedEntity<Guid>>
    {
        public int SolutionId { get; set; }
        [DisplayName("Name")]
        public string Name { get; set; }
        [DisplayName("Display Name")]
        public string DisplayName { get; set; }
        public IList<CreateEnumValue> Values { get; set; }
        public PropertyCollection Properties { get; set; }
    }
}
