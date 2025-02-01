﻿using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Mango.Services.ShoppingCartAPI.Models.Dto;

namespace Mango.Services.ShoppingCartAPI.Models
{
    public class CartDetails
    {
        [Key]
        public int CartDetailId {  get; set; }
        public int CartHeaderId {  get; set; }
        [ForeignKey("CartHeaderId")]
        public CartHeader? CardHeader { get; set; }
        public int ProductId {  get; set; }
        [NotMapped]
        public ProductDto? Product { get; set; }

        public int Count {  get; set; }
    }
}
